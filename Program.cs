using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;


namespace BeameWindowsInstaller
{
    class Program
    {
        const string openSSLPath = "C:\\OpenSSL-Win64";
        const string openSSLInstaller = "OpenSSL-Win64.zip";
        const string gitInstaller = "Git-2.11.0-64-bit.exe";
        const string nodeInstaller = "node-v8.9.3-x64.msi";
        static string progFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        enum InstallerOptions
        {
            Gatekeeper = 1,
            BeameSDK = 2,
            Exit = 9
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Beame.io Windows Installer");
            Console.WriteLine();

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                Console.WriteLine("Using Programs file folder: " + progFolder);
                Console.WriteLine();
                foreach (InstallerOptions option in Enum.GetValues(typeof(InstallerOptions)))
                {
                    Console.WriteLine(String.Format("{0}. {1}", (int)option, option));
                }
                Console.WriteLine("");
                Console.WriteLine("Please enter option:");
                string selected = Console.ReadLine();
                Int32.TryParse(selected, out int opt);

                if (opt != (int)InstallerOptions.Gatekeeper && opt != (int)InstallerOptions.BeameSDK)
                    Environment.Exit(0);

                if (!InstallGit() || !InstallNode() || !InstallOpenSSL())
                {
                    Console.ReadLine();
                    Environment.Exit(Environment.ExitCode);
                }

                bool installed = opt == (int)InstallerOptions.Gatekeeper ? InstallBeameGateKeeper() : InstallBeameSDK();
                Console.WriteLine();
                if (installed)
                {
                    Console.WriteLine("Installer finished successfully");
                    Console.WriteLine();
                    Process.Start(opt == (int)InstallerOptions.Gatekeeper ? "https://ypxf72akb6onjvrq.ohkv8odznwh5jpwm.v1.p.beameio.net/gatekeeper" : "https://ypxf72akb6onjvrq.ohkv8odznwh5jpwm.v1.p.beameio.net/");
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
            Console.WriteLine("Installing Beame.io Gatekeeper...");

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

            return result;
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
                }

                if (result)
                {
                    //run NPM upgrade
                    Console.WriteLine("Updating npm packages");
                    string npmPath = Path.Combine(nodeJSPath, "npm.cmd");
                    result = StartAndCheckReturn(npmPath, "install -g --production windows-build-tools");
                    result = StartAndCheckReturn(npmPath, "install -g npm@latest");
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