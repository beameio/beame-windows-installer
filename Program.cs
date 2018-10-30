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
        static readonly string proxyAddressProtocol = ConfigurationManager.AppSettings["ProxyAddressProtocol"];
        static readonly string proxyAddressFqdn = ConfigurationManager.AppSettings["ProxyAddressAddress"];
        static readonly string proxyAddressPort = ConfigurationManager.AppSettings["ProxyAddressPort"];
        static readonly string proxyAddressExcludes = ConfigurationManager.AppSettings["ProxyAddressExcludes"];
        static readonly string externalOcspServerFqdn = ConfigurationManager.AppSettings["ExternalOcspServerFqdn"];
        static readonly string proxyAddress = string.IsNullOrWhiteSpace(proxyAddressFqdn) 
                ? "" 
                : proxyAddressProtocol + "://" +  proxyAddressFqdn + (string.IsNullOrWhiteSpace(proxyAddressPort) ? "" : ":" + proxyAddressPort);

        static readonly string customGatekeeper = ConfigurationManager.AppSettings["CustomGatekeeper"];
        static readonly string customGatekeeperCSS = ConfigurationManager.AppSettings["CustomGatekeeperCSS"];

        static string progFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        static string homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
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

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
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

                InstallerOptions opt;
                Enum.TryParse(selected, out opt);
                bool installed = false;
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
            bool result = false;

            Console.WriteLine("--> Installing Beame.io Gatekeeper from npm master");
            string nodeJSPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
            string npmPath = Path.Combine(nodeJSPath, "npm.cmd");
            string nodePath = Path.Combine(nodeJSPath, "node.exe");
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
 
            if (!string.IsNullOrWhiteSpace(customGatekeeperCSS) || !string.IsNullOrWhiteSpace(customGatekeeper))
            {
                var gatekeeperPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"npm\node_modules\beame-gatekeeper");

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
                if (!string.IsNullOrWhiteSpace(customGatekeeper))
                {
                    Console.WriteLine("--> Installing custom Beame.io Gatekeeper from " + customGatekeeper);
                    using (var strm = File.OpenRead(customGatekeeper))
                    using (var a = new ZipArchive(strm))
                    {
                        a.Entries.Where(o => o.Name == string.Empty && !Directory.Exists(Path.Combine(gatekeeperPath, o.FullName))).ToList().ForEach(o => Directory.CreateDirectory(Path.Combine(gatekeeperPath, o.FullName)));
                        a.Entries.Where(o => o.Name != string.Empty).ToList().ForEach(e => e.ExtractToFile(Path.Combine(gatekeeperPath, e.FullName), true));
                    }
                }

                Helper.StartAndCheckReturn(npmPath, "install", false, "", 10, gatekeeperPath);
                Helper.StartAndCheckReturn(nodePath, @"node_modules\gulp\bin\gulp.js sass web_sass compile", false, "", 10, gatekeeperPath);
            }

            AddProxySettingsToBeame();
            
            Console.WriteLine("--> creating windows service");
            Helper.StartAndCheckReturn("sc.exe", "create \"Beame Gatekeeper\" binpath= \"\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\beame-gatekeeper.cmd") + "\" server start\" start= auto");
           
            return result;
        }


        private static bool InstallBeameSDK()
        {
            bool result = false;
            Console.WriteLine("Installing Beame.io SDK...");

            string nodeJSPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
            string npmPath = Path.Combine(nodeJSPath, "npm.cmd");
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

            AddProxySettingsToBeame();
            return result;
        }

        private static void AddProxySettingsToBeame()
        {
            // set git proxy
            if (!string.IsNullOrWhiteSpace(proxyAddress))
            {
                string file = Path.Combine(homeFolder, @".beame_server\config\app_config.json");
                Console.WriteLine("--> Changing proxy settings in file " + file);

                string json = File.ReadAllText(file);
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                jsonObj["ProxySettings"]["host"] = proxyAddressFqdn;
                jsonObj["ProxySettings"]["port"] = proxyAddressPort;
                jsonObj["ProxySettings"]["excludes"] = proxyAddressExcludes;
                jsonObj["ExternalOcspServerFqdn"] = externalOcspServerFqdn;
                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(file, output);
                
                Console.WriteLine("--> Setting cmdline proxy");
                Helper.SetEnv("HTTP_PROXY", proxyAddress);
                Helper.SetEnv("HTTPS_PROXY", proxyAddress);
                Helper.SetEnv("FTP_PROXY", proxyAddress);
                Helper.SetEnv("NO_PROXY", proxyAddressExcludes);
            }
        }

        private static void InstallDeps() {
            if (!InstallOpenSSL() || !InstallGit() || !InstallNode())
            {
                Console.ReadLine();
                Environment.Exit(Environment.ExitCode);
            }
        }
        private static bool InstallGit()
        {
            Console.WriteLine("Installing Git...");
            bool result = true;

            //check for GIT and install it if necessary
            string gitPath = Path.Combine(progFolder, "Git");
            if (!Directory.Exists(gitPath) || !File.Exists(Path.Combine(gitPath, @"bin\git.exe")))
            {
                string exePath = Path.Combine(Path.GetTempPath(), gitInstaller);
                Helper.WriteResourceToFile(gitInstaller, exePath);

                result = Helper.StartAndCheckReturn(exePath, "/VERYSILENT /CLOSEAPPLICATIONS /NORESTART");
                Console.WriteLine("Git installation " + (result ? "suceeded" : "failed"));
            }
            else
            {
                Console.WriteLine("Git already installed");
            }

            // set git proxy if defined
            if (!string.IsNullOrWhiteSpace(proxyAddress))
            {                
                string gitcmd = Path.Combine(gitPath, @"bin\git.exe");
                Helper.StartAndCheckReturn(gitcmd, "config --global http.proxy " + proxyAddress);
                Helper.StartAndCheckReturn(gitcmd, "config --global https.proxy " + proxyAddress);
            }
            return result;
        }

        private static bool InstallNode()
        {
            Console.WriteLine("Installing NodeJS...");
            bool result = true;
            string msiPath = Path.Combine(Path.GetTempPath(), "nodeJS.msi");

            try
            {
                string nodeJSPath = Path.Combine(progFolder, "nodejs");

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
                }

                if (result)
                {
                    Console.WriteLine("Updating npm packages");
                    string npmPath = Path.Combine(nodeJSPath, "npm.cmd");

                    // set npm proxy if defined
                    if (!string.IsNullOrWhiteSpace(proxyAddress))
                    {
                        Helper.StartAndCheckReturn(npmPath, "config set proxy " + proxyAddress);
                        Helper.StartAndCheckReturn(npmPath, "config set https-proxy " + proxyAddress);
                    }

                    //run NPM upgrade
                    result = Helper.StartAndCheckReturn(npmPath, "install -g npm@latest")
                             && Helper.StartAndCheckReturn(npmPath, "install -g --production --add-python-to-path='true' windows-build-tools");
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
                
                string openSSLFile = Path.Combine(openSSLPath, @"bin\openssl.exe");
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
                    Environment.SetEnvironmentVariable("OPENSSL_CONF", Path.Combine(openSSLPath, @"ssl\openssl.cnf"), EnvironmentVariableTarget.Machine);
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
    }
}