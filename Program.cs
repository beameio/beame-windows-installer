using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Configuration;
using System.Security.Principal;
using System.Linq;

namespace BeameWindowsInstaller
{
    static class Program
    {
        private const string openSSLInstaller = "OpenSSL-Win64.zip";
        private const string gitInstaller = "Git-2.11.0-64-bit.exe";
        private const string nodeInstaller = "node-v8.12.0-x64.msi";
        private const string nssmInstaller = "nssm.exe";

        private static readonly string progFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        private static readonly string homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static readonly string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        
        private static readonly string nssmPath = Path.Combine(progFolder, "nssm");
        private static readonly string nssmFile = Path.Combine(nssmPath, nssmInstaller);
        
        private static readonly string registerSiteOnFinish = Helper.GetConfigurationValue("RegisterSiteOnFinish");
        private static readonly bool enableRegisterSiteOnFinish = Helper.GetConfigurationValue("EnableRegisterSiteOnFinish", true);
        
        private static readonly string proxyAddressProtocol = Helper.GetConfigurationValue("ProxyAddressProtocol");
        private static readonly string proxyAddressFqdn = Helper.GetConfigurationValue("ProxyAddressFqdn");
        private static readonly string proxyAddressPort = Helper.GetConfigurationValue("ProxyAddressPort");
        private static readonly string proxyAddressExcludes = Helper.GetConfigurationValue("ProxyAddressExcludes");
        private static readonly string externalOcspServerFqdn = Helper.GetConfigurationValue("ExternalOcspServerFqdn");
        private static readonly string proxyAddress = string.IsNullOrWhiteSpace(proxyAddressFqdn) 
            ? "" 
            : proxyAddressProtocol + "://" +  proxyAddressFqdn + (string.IsNullOrWhiteSpace(proxyAddressPort) ? "" : ":" + proxyAddressPort);

        private static readonly string gatekeeperName = Helper.GetConfigurationValue("GatekeeperName", "Beame Gatekeeper");
        private static readonly string gatekeeperMode = Helper.GetConfigurationValue("GatekeeperMode", "Gatekeeper");
        private static readonly bool encryptUserData = Helper.GetConfigurationValue("EncryptUserData", true);
        private static readonly bool allowDirectSignin = Helper.GetConfigurationValue("AllowDirectSignin", true);
        private static readonly bool publicRegistration = Helper.GetConfigurationValue("PublicRegistration", false);
        private static readonly bool registrationImageRequired = Helper.GetConfigurationValue("RegistrationImageRequired", false);
        private static readonly bool allowSignInWithCreds = Helper.GetConfigurationValue("AllowSignInWithCreds", true);
        private static readonly bool allowSignInWithUltrasound = Helper.GetConfigurationValue("AllowSignInWithUltrasound", true);
        private static readonly bool disableDemoServers = Helper.GetConfigurationValue("DisableDemoServers", false);
        private static readonly bool advanceSettingsEnabled = Helper.GetConfigurationValue("AdvanceSettingsEnabled", false);
        private static readonly bool showZendeskSupport = Helper.GetConfigurationValue("ShowZendeskSupport", false);

        private static readonly string customGatekeeper = Helper.GetConfigurationValue("CustomGatekeeper");
        private static readonly string customGatekeeperCSS = Helper.GetConfigurationValue("CustomGatekeeperCSS");
        
        static readonly string windowsServiceGatekeeperName = "Beame Gatekeeper";

        enum InstallerOptions
        {
            Gatekeeper = 1,
            BeameSDK = 2,
            Dependencies = 7,
            Exit = 9
        }
        
        static void Main(string[] args)
        {
            Console.WriteLine("Beame.io Windows Installer");
            Console.WriteLine("**************************");
            Console.WriteLine("Note: install dependencies before any other software");
            Console.WriteLine();

            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                string selected;
                if (args.Length > 1)
                {
                    selected = args[1];
                }
                else
                {
                    Console.WriteLine("Using Programs file folder: " + progFolder);
                    Console.WriteLine();
                    foreach (InstallerOptions option in Enum.GetValues(typeof(InstallerOptions)))
                    {
                        Console.WriteLine("{0}. {1}", (int)option, option);
                    }
                    Console.WriteLine("");
                    Console.WriteLine("Please enter option:");
                    selected = Console.ReadLine();
                }

                Enum.TryParse(selected, out InstallerOptions opt);
                var installed = false;
                switch(opt)
                {
                    case InstallerOptions.Gatekeeper:
                        InstallDeps();
                        installed = InstallBeameGateKeeper();
                        break;

                    case InstallerOptions.BeameSDK:
                        InstallDeps();
                        installed = InstallBeameSDK();
                        break;
                    
                    case InstallerOptions.Dependencies:
                        InstallDeps();
                        break;
                    default:
                        Environment.Exit(0);
                        break;
                }

                Console.WriteLine();
                if (installed)
                {
                    Console.WriteLine("== Installer finished successfully ==");
                    Console.WriteLine();
                    if (enableRegisterSiteOnFinish)
                    {
                        Process.Start(opt == InstallerOptions.Gatekeeper
                            ? registerSiteOnFinish + "/gatekeeper"
                            : registerSiteOnFinish);
                        Console.WriteLine();
                        Console.WriteLine(
                            "Please fill the name and email in the registration form of the opened webpage");
                        Console.WriteLine(
                            "After receiving the instruction email please follow only the last section (\"For Windows...\")");
                    }

                    Console.WriteLine();
                    Console.WriteLine("Once the credentials are installed, start the windows service '"+ windowsServiceGatekeeperName +"' or by running 'sc.exe start \"" + windowsServiceGatekeeperName + "\"'");
                    Console.WriteLine();
                    Console.WriteLine("Press a key to exit");
                    Console.ReadLine();
                }
                else
                {
                    Console.Write("Installer failed");
                    Console.WriteLine();
                    Console.ReadLine();
                }
            }
            else
            {
                Console.WriteLine("Please run the installer with administrative rights");
                Console.WriteLine();
                Console.ReadLine();
            }
        }


        private static bool InstallBeameGateKeeper()
        {
            var result = false;

            Console.WriteLine("--> Installing Beame.io Gatekeeper from npm master");
            var nodeJSPath = Path.Combine(progFolder, "nodejs");
            var npmPath = Path.Combine(nodeJSPath, "npm.cmd");
            var nodePath = Path.Combine(nodeJSPath, "node.exe");
            

            var gatekeeperPath = Path.Combine(appDataFolder, @"npm\node_modules\beame-gatekeeper");

            if (string.IsNullOrWhiteSpace(customGatekeeper))
            {
                try
                {
                    //add GIT to path before starting this installation, in case GIT was just recently installed
                    result = Helper.StartAndCheckReturn(npmPath, "install -g beame-gatekeeper", false, @"C:\Program Files\Git\cmd");
                    Console.WriteLine("Beame.io Gatekeeper installation " + (result ? "suceeded" : "failed"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Beame.io Gatekeeper installation failed - {0}", ex.Message);
                }
            }
            else
            {
                // If custom gatekeeper remove gatekeeper directory and add custom one
                Console.WriteLine("--> Installing custom Beame.io Gatekeeper from " + customGatekeeper);
                if (Directory.Exists(gatekeeperPath))
                {
                    var dir = new DirectoryInfo(gatekeeperPath);
                    dir.Delete(true);
                }

                var customGatekeeperFolder = Path.Combine(progFolder, "beame-gatekeeper");
                if (Directory.Exists(customGatekeeperFolder))
                {
                    var dir = new DirectoryInfo(customGatekeeperFolder);
                    dir.Delete(true);
                }

                ZipFile.ExtractToDirectory(customGatekeeper, customGatekeeperFolder);
                result = Helper.StartAndCheckReturn(npmPath, "install -g beame-gatekeeper", false, @"C:\Program Files\Git\cmd", 600, progFolder);
            }

            if (!string.IsNullOrWhiteSpace(customGatekeeperCSS))
            {
                Console.WriteLine("--> Adding custom css to Beame.io Gatekeeper from " + customGatekeeperCSS);
                using (var strm = File.OpenRead(customGatekeeperCSS))
                using (var a = new ZipArchive(strm))
                {
                    a.Entries.Where(o => o.Name == string.Empty && !Directory.Exists(Path.Combine(gatekeeperPath, o.FullName))).ToList().ForEach(o => Directory.CreateDirectory(Path.Combine(gatekeeperPath, o.FullName)));
                    a.Entries.Where(o => o.Name != string.Empty).ToList().ForEach(e => e.ExtractToFile(Path.Combine(gatekeeperPath, e.FullName), true));
                }
            }
            
            if (!string.IsNullOrWhiteSpace(customGatekeeperCSS) || !string.IsNullOrWhiteSpace(customGatekeeper))
            {
                Console.WriteLine("--> Installing custom Beame.io Gatekeeper");

                // Make install and gulp if any custom was applied
                result = result && Helper.StartAndCheckReturn(npmPath, "install", false, @"C:\Program Files\Git\cmd", 600, gatekeeperPath) &&
                         Helper.StartAndCheckReturn(nodePath, @"node_modules\gulp\bin\gulp.js sass web_sass compile", false, @"C:\Program Files\Git\cmd", 600, gatekeeperPath);
            }

            ChangeGatekeeperSettings();
            
            Console.WriteLine("--> creating windows service");
            Helper.StartAndCheckReturn(nssmFile, "remove \"" + windowsServiceGatekeeperName + "\" confirm");
            Helper.StartAndCheckReturn(nssmFile, "install \"" + windowsServiceGatekeeperName + "\" \"" + Path.Combine(appDataFolder, @"npm\beame-gatekeeper.cmd") + "\" server start");
            
            var userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            Console.WriteLine("Please insert current user " +  userName + " password:");
            var password = Console.ReadLine(); 
            Helper.StartAndCheckReturn(nssmFile, "set \"" + windowsServiceGatekeeperName + "\" ObjectName " + userName + " " + password);

            return result;
        }

        private static void ChangeGatekeeperSettings()
        {
            var path = Path.Combine(homeFolder, @".beame_server\config\");
            var file = Path.Combine(path, "app_config.json");

            // if configs dont exist, initialize them
            if (!Directory.Exists(path))
            {
                Helper.StartAndCheckReturn(Path.Combine(appDataFolder, @"npm\beame-gatekeeper.cmd"), "server config", false, Path.Combine(progFolder, "nodejs"), 20);
            }

            Console.WriteLine("--> Changing settings in gatekeeper file " + file);
            var json = File.ReadAllText(file);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            jsonObj["ServiceName"] = gatekeeperName;
            jsonObj["EnvMode"] = gatekeeperMode;
            jsonObj["HtmlEnvMode"] = "Prod";
            
            jsonObj["EncryptUserData"] = encryptUserData;
            jsonObj["AllowDirectSignin"] = allowDirectSignin;
            jsonObj["PublicRegistration"] = publicRegistration;
            jsonObj["RegistrationImageRequired"] = registrationImageRequired;
            jsonObj["AllowSignInWithCreds"] = allowSignInWithCreds;
            jsonObj["AllowSignInWithUltrasound"] = allowSignInWithUltrasound;
            jsonObj["DisableDemoServers"] = disableDemoServers;
            jsonObj["AdvanceSettingsEnabled"] = advanceSettingsEnabled;
            jsonObj["ShowZendeskSupport"] = showZendeskSupport;
            
            if (!string.IsNullOrWhiteSpace(proxyAddress))
            {
                jsonObj["ProxySettings"]["host"] = proxyAddressFqdn;
                jsonObj["ProxySettings"]["port"] = proxyAddressPort;
                jsonObj["ProxySettings"]["excludes"] = proxyAddressExcludes;
                jsonObj["ExternalOcspServerFqdn"] = externalOcspServerFqdn;
            }
            var output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(file, output);
        }

        private static bool InstallBeameSDK()
        {
            var result = false;
            Console.WriteLine("Installing Beame.io SDK...");

            var nodeJSPath = Path.Combine(progFolder, "nodejs");
            var npmPath = Path.Combine(nodeJSPath, "npm.cmd");
            try
            {
                //add GIT to path before starting this installation, in case GIT was just recently installed
                result = Helper.StartAndCheckReturn(npmPath, "install -g beame-sdk", false, @"C:\Program Files\Git\cmd");
                Console.WriteLine("Beame.io SDK installation " + (result ? "suceeded" : "failed"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Beame.io SDK installation failed - {0}", ex.Message);
            }

            return result;
        }
        
        #region dependencies
        private static void InstallDeps() {
            if (!InstallNSSM() || !InstallOpenSSL() || !InstallGit() || !InstallNode())
            {
                Console.ReadLine();
                Environment.Exit(Environment.ExitCode);
            }

            // set env proxy
            if (!string.IsNullOrWhiteSpace(proxyAddress)) {
                Console.WriteLine("--> Setting cmdline proxy");
                Helper.SetEnv("HTTP_PROXY", proxyAddress);
                Helper.SetEnv("HTTPS_PROXY", proxyAddress);
                Helper.SetEnv("FTP_PROXY", proxyAddress);
                Helper.SetEnv("NO_PROXY", proxyAddressExcludes);
            }
        }
        
        private static bool InstallGit()
        {
            Console.WriteLine("Installing Git...");
            var result = true;

            //check for GIT and install it if necessary
            var gitPath = Path.Combine(progFolder, "Git");
            var gitcmd = Path.Combine(gitPath, @"cmd\git.exe");
            if (!Directory.Exists(gitPath) || !File.Exists(gitcmd))
            {
                string exePath = Path.Combine(Path.GetTempPath(), gitInstaller);
                Helper.WriteResourceToFile(gitInstaller, exePath);

                result = Helper.StartAndCheckReturn(exePath, "/VERYSILENT /CLOSEAPPLICATIONS /NORESTART");
                Helper.AddToPath(Path.Combine(gitPath, @"cmd"));
                Console.WriteLine("Git installation " + (result ? "suceeded" : "failed"));
            }
            else
            {
                Console.WriteLine("Git already installed");
            }

            // set git proxy if defined
            if (!string.IsNullOrWhiteSpace(proxyAddress))
            {
                Helper.StartAndCheckReturn(gitcmd, "config --global http.proxy " + proxyAddress);
                Helper.StartAndCheckReturn(gitcmd, "config --global https.proxy " + proxyAddress);
            }
            return result;
        }

        private static bool InstallNSSM()
        {
            Console.WriteLine("Installing NSSM...");
            var result = true;
            
            if (!Directory.Exists(nssmPath))
            {
                Directory.CreateDirectory(nssmPath);
            }
            
            if (File.Exists(nssmFile))
            {
                Console.WriteLine("Already installed...");
            }
            else
            {
                Helper.WriteResourceToFile(nssmInstaller, nssmFile);
            }

            return result;
        }

        private static bool InstallNode()
        {
            Console.WriteLine("Installing NodeJS...");
            var result = true;
            var msiPath = Path.Combine(Path.GetTempPath(), "nodeJS.msi");

            try
            {
                var nodeJSPath = Path.Combine(progFolder, "nodejs");
                var npmPath = Path.Combine(nodeJSPath, "npm.cmd");

                //check if NPM and node.exe exist
                if (File.Exists(Path.Combine(nodeJSPath, "npm.cmd")) && File.Exists(Path.Combine(nodeJSPath, "node.exe")))
                {
                    Console.WriteLine("Already installed...");
                }
                else
                {
                    Helper.WriteResourceToFile(nodeInstaller, msiPath);
                    result = Helper.StartAndCheckReturn("msiexec", "/i " + msiPath + " /quiet /qn /norestart");
                    Console.WriteLine("NodeJS installation " + (result ? "suceeded" : "failed"));

                    Helper.AddToPath(nodeJSPath);
                    Helper.AddToPath(Path.Combine(appDataFolder, @"npm\"));
                }

                if (result)
                {
                    // set npm proxy if defined
                    if (!string.IsNullOrWhiteSpace(proxyAddress))
                    {
                        Helper.StartAndCheckReturn(npmPath, "config set proxy " + proxyAddress);
                        Helper.StartAndCheckReturn(npmPath, "config set https-proxy " + proxyAddress);
                    }
                    
                    Console.WriteLine("Updating npm packages");
                        
                    //run NPM upgrade
                    result = Helper.StartAndCheckReturn(npmPath, "install -g npm@latest")
                             && Helper.StartAndCheckReturn(npmPath, "install -g node-gyp") 
                             && Helper.StartAndCheckReturn(npmPath, "install -g --production --scripts-prepend-node-path=true --add-python-to-path='true' windows-build-tools");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("NodeJS installation error - {0}", ex.Message);
                result = false;
            }
            finally
            {
                Helper.RemoveFile(msiPath);
            }
            return result;
        }

        private static bool InstallOpenSSL()
        {
            Console.WriteLine("Installing OpenSSL...");
            var openSSLPath = Path.Combine(progFolder,"OpenSSL-Win64");
            try
            {
                if (!Directory.Exists(openSSLPath))
                {
                    Directory.CreateDirectory(openSSLPath);
                }
                
                var openSSLFile = Path.Combine(openSSLPath, @"bin\openssl.exe");
                if (File.Exists(openSSLFile))
                {
                    Console.WriteLine("Already exists...");
                }
                else
                {
                    string tmpPath = Path.Combine(Path.GetTempPath(), openSSLInstaller);
                    Helper.WriteResourceToFile(openSSLInstaller, tmpPath);

                    Console.WriteLine("extracting files...");
                    ZipFile.ExtractToDirectory(tmpPath, progFolder);
                    Helper.SetEnv("OPENSSL_CONF", Path.Combine(openSSLPath, @"ssl\openssl.cnf"));
                    Helper.AddToPath(Path.Combine(openSSLPath, @"bin\"));
                }

                Console.WriteLine("OK");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed - {0}", ex.Message);
                return false;
            }
        }
        #endregion
    }
}