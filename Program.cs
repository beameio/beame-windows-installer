using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Configuration;
using System.Security.Principal;

namespace BeameWindowsInstaller
{
    static class Program
    {
        const string openSSLPath = "C:\\OpenSSL-Win64";
        const string openSSLInstaller = "OpenSSL-Win64.zip";
        const string gitInstaller = "Git-2.11.0-64-bit.exe";
        const string nodeInstaller = "node-v8.12.0-x64.msi";

        static readonly string proxyAddressProtocol = ConfigurationManager.AppSettings["ProxyAddressProtocol"];
        static readonly string proxyAddressAddress = ConfigurationManager.AppSettings["ProxyAddressAddress"];
        static readonly string proxyAddressPort = ConfigurationManager.AppSettings["ProxyAddressPort"];
        static readonly string proxyAddressExcludes = ConfigurationManager.AppSettings["ProxyAddressExcludes"];
        static readonly string proxyAddress = string.IsNullOrWhiteSpace(proxyAddressProtocol) ? "" : proxyAddressProtocol + "://" +  proxyAddressAddress + (string.IsNullOrWhiteSpace(proxyAddressPort) ? "" : ":" + proxyAddressPort);

        static readonly string customGatekeeper = ConfigurationManager.AppSettings["CustomGatekeeper"];
        static readonly string customGatekeeperCSS = ConfigurationManager.AppSettings["CustomGatekeeperCSS"];
        static readonly string gatekeeperInstallationPath = ConfigurationManager.AppSettings["GatekeeperInstallationPath"];

        static string progFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

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



            // TODO
            //   Confirm dependencies install in one go
            //   Make dependencies installation silent


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

            if (String.IsNullOrWhiteSpace(customGatekeeper))
            {
                Console.WriteLine("--> Installing Beame.io Gatekeeper from npm master");
                string nodeJSPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
                string npmPath = Path.Combine(nodeJSPath, "npm.cmd");
                try
                {
                    //add GIT to path before starting this installation, in case GIT was just recently installed
                    result = StartAndCheckReturn(npmPath, "install -g beame-gatekeeper", false, "C:\\Program Files\\Git\\cmd");
                    Console.WriteLine("Beame.io Gatekeeper installation " + (result ? "suceeded" : "failed"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Beame.io Gatekeeper installation failed - {0}", ex.Message);
                }
            }
            else
            {
                Console.WriteLine("--> Installing custom Beame.io Gatekeeper from " + customGatekeeper);
                // TODO

            }


            if(!String.IsNullOrWhiteSpace(customGatekeeperCSS))
            {
                Console.WriteLine("--> Adding custom css to Beame.io Gatekeeper");
                // TODO

            }

            AddProxySettingsToBeame();
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
                result = StartAndCheckReturn(npmPath, "install -g beame-sdk", false, "C:\\Program Files\\Git\\cmd");
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
            if (!String.IsNullOrWhiteSpace(proxyAddress))
            {
                // TODO 
                // add to ~/.beame_server/config/app_config.json   
                //   "ProxySettings": {
                //       "host": "descproxy01.brainlab.net",
                //         "port": 8080,
                //         "excludes": "127.0.0.1,localhost"
                //   },
                //  "ExternalOcspServerFqdn": "iep9bs1p7cj3cmit.tl5h1ipgobrdqsj6.v1.p.beameio.net“,


                // add HTTP_PROXY, HTTPS_PROXY, FTP_PROXY
            }
        }

        private static void InstallDeps() {
            if (!InstallGit() || !InstallNode() || !InstallOpenSSL())
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
            if (!Directory.Exists(gitPath) || !File.Exists(Path.Combine(gitPath, "git-cmd.exe")))
            {
                string exePath = Path.Combine(Path.GetTempPath(), gitInstaller);
                WriteResourceToFile(gitInstaller, exePath);

                result = StartAndCheckReturn(exePath, "");
                Console.WriteLine("Git installation " + (result ? "suceeded" : "failed"));
            }
            else
            {
                Console.WriteLine("Git already installed");
            }

            // set git proxy
            if (!String.IsNullOrWhiteSpace(proxyAddress))
            {
                string gitcmd = Path.Combine(gitPath, "git-cmd.exe");
                StartAndCheckReturn(gitcmd, "config --global http.proxy " + proxyAddress);
                StartAndCheckReturn(gitcmd, "config --global https.proxy " + proxyAddress);
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
                    WriteResourceToFile(nodeInstaller, msiPath);
                    result = StartAndCheckReturn("msiexec", "/i " + msiPath);
                    Console.WriteLine("NodeJS installation " + (result ? "suceeded" : "failed"));

                    // TODO: when node installed it seems that a relogin is required before proceeding in order to reload the environment -- find a way to handle this
                }

                if (result)
                {
                    Console.WriteLine("Updating npm packages");
                    string npmPath = Path.Combine(nodeJSPath, "npm.cmd");

                    // set npm proxy
                    if (!String.IsNullOrWhiteSpace(proxyAddress))
                    {
                        StartAndCheckReturn(npmPath, "config set proxy " + proxyAddress);
                        StartAndCheckReturn(npmPath, "config set https-proxy " + proxyAddress);
                    }

                    //run NPM upgrade
                    result = StartAndCheckReturn(npmPath, "install -g npm@latest");
                    result = StartAndCheckReturn(npmPath, "install -g --production windows-build-tools");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("NodeJS installation error - {0}", ex.Message);
                result = false;
            }
            finally
            {
                RemoveFile(msiPath);
            }

            return result;
        }

        private static void RemoveFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            { }
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
                
                string openSSLFile = Path.Combine(openSSLPath, "bin/openssl.exe");
                if (File.Exists(openSSLFile))
                {
                    Console.WriteLine("Already exists...");
                }
                else
                {
                    string tmpPath = Path.Combine(Path.GetTempPath(), openSSLInstaller);
                    WriteResourceToFile(openSSLInstaller, tmpPath);

                    Console.WriteLine("extracting files...");
                    ZipFile.ExtractToDirectory(tmpPath, "c:/");
                    Environment.SetEnvironmentVariable("OPENSSL_CONF", Path.Combine(openSSLPath, "ssl/openssl.cnf"), EnvironmentVariableTarget.Machine);
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

        static void WriteResourceToFile(string resourceName, string fileName)
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("BeameWindowsInstaller.Resources." + resourceName))
            {
                using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    resource.CopyTo(file);
                }
            }
        }

        static bool StartAndCheckReturn(string fileName, string arguments, bool useShellExecute = false, string addToPath = "", int timeoutMinutes = 10)
        {

            var procStartInfo = new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = useShellExecute,
            };
            if (!string.IsNullOrEmpty(addToPath))
            {
                string envPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                envPath += ";" + addToPath;
                procStartInfo.EnvironmentVariables["PATH"] = envPath;
            }
            var proc = Process.Start(procStartInfo);

            bool timedOut = !proc.WaitForExit(timeoutMinutes * 60 * 1000);

            if (proc.ExitCode < 0)
            {
                Environment.ExitCode = proc.ExitCode;
            }

            return !timedOut && proc.ExitCode == 0;
        }
    }
}