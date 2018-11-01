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
        const string openSSLInstaller = "OpenSSL-Win64.zip";
        const string gitInstaller = "Git-2.11.0-64-bit.exe";
        const string nodeInstaller = "node-v8.12.0-x64.msi";

        static readonly string openSSLPath = ConfigurationManager.AppSettings["OpenSSLPath"];
        static readonly string gatekeeperName = ConfigurationManager.AppSettings["GatekeeperName"];
        static readonly string gatekeeperMode = ConfigurationManager.AppSettings["GatekeeperMode"];
        static readonly string proxyAddressProtocol = ConfigurationManager.AppSettings["ProxyAddressProtocol"];
        static readonly string proxyAddressFqdn = ConfigurationManager.AppSettings["ProxyAddressFqdn"];
        static readonly string proxyAddressPort = ConfigurationManager.AppSettings["ProxyAddressPort"];
        static readonly string proxyAddressExcludes = ConfigurationManager.AppSettings["ProxyAddressExcludes"];
        static readonly string externalOcspServerFqdn = ConfigurationManager.AppSettings["ExternalOcspServerFqdn"];
        static readonly string proxyAddress = string.IsNullOrWhiteSpace(proxyAddressFqdn) 
                ? "" 
                : proxyAddressProtocol + "://" +  proxyAddressFqdn + (string.IsNullOrWhiteSpace(proxyAddressPort) ? "" : ":" + proxyAddressPort);

        static readonly string customGatekeeper = ConfigurationManager.AppSettings["CustomGatekeeper"];
        static readonly string customGatekeeperCSS = ConfigurationManager.AppSettings["CustomGatekeeperCSS"];
        
        static readonly string progFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        static readonly string homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
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
                        Console.WriteLine(String.Format("{0}. {1}", (int)option, option));
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
                    Console.WriteLine("Installer finished successfully");
                    Console.WriteLine();
                    Process.Start(opt == InstallerOptions.Gatekeeper ? "https://ypxf72akb6onjvrq.ohkv8odznwh5jpwm.v1.p.beameio.net/gatekeeper" : "https://ypxf72akb6onjvrq.ohkv8odznwh5jpwm.v1.p.beameio.net/");
                    Console.WriteLine("Please fill the administrator name and email in the registration form.");
                    Console.WriteLine("After receiving the instruction email please follow only the last section (\"For Windows...\")");
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
            var nodeJSPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
            var npmPath = Path.Combine(nodeJSPath, "npm.cmd");
            var nodePath = Path.Combine(nodeJSPath, "node.exe");
            var gatekeeperPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"npm\node_modules\beame-gatekeeper");

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

            Console.WriteLine("--> creating windows service");
            Helper.StartAndCheckReturn("sc.exe", "delete \"Beame Gatekeeper\"");
            Helper.StartAndCheckReturn("sc.exe", "create \"Beame Gatekeeper\" binpath= \"\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\beame-gatekeeper.cmd") + "\" server start\" start= auto");

            if (!string.IsNullOrWhiteSpace(customGatekeeper))
            {
                // If custom gatekeeper remove gatekeeper directory and add custom one
                Console.WriteLine("--> Installing custom Beame.io Gatekeeper from " + customGatekeeper);
                if (Directory.Exists(gatekeeperPath))
                {
                    var dir = new DirectoryInfo(gatekeeperPath);
                    dir.Delete(true);
                }
                ZipFile.ExtractToDirectory(customGatekeeper, gatekeeperPath);
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
                result = result && Helper.StartAndCheckReturn(npmPath, "install", false, "", 10, gatekeeperPath) &&
                         Helper.StartAndCheckReturn(nodePath, @"node_modules\gulp\bin\gulp.js sass web_sass compile", false, "", 10, gatekeeperPath);
            }

            ChangeGatekeeperSettings();
            return result;
        }

        private static void ChangeGatekeeperSettings()
        {
            var file = Path.Combine(homeFolder, @".beame_server\config\app_config.json");

            Console.WriteLine("--> Changing settings in gatekeeper file " + file);
            var json = File.ReadAllText(file);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            jsonObj["ServiceName"] = gatekeeperName;
            jsonObj["EnvMode"] = gatekeeperMode;
            jsonObj["HtmlEnvMode"] = "Prod";
            jsonObj["EncryptUserData"] = true;
            jsonObj["ShowZendeskSupport"] = false;
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

            var nodeJSPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
            var npmPath = Path.Combine(nodeJSPath, "npm.cmd");
            try
            {
                //add GIT to path before starting this installation, in case GIT was just recently installed
                result = Helper.StartAndCheckReturn(npmPath, "install -g beame-sdk", false, "C:\\Program Files\\Git\\cmd");
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
            if (!InstallOpenSSL() || !InstallGit() || !InstallNode())
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
                    Helper.AddToPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\"));
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
            Console.Write("Installing OpenSSL...");

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
                    ZipFile.ExtractToDirectory(tmpPath, @"c:\");
                    Helper.SetEnv("OPENSSL_CONF", Path.Combine(openSSLPath, @"ssl\openssl.cnf"));
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