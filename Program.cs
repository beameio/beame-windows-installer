﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Principal;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BeameWindowsInstaller
{
    static class Program
    {
        [DllImport("wininet.dll")]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;
        
        private const string openSSLInstaller = "OpenSSL-Win64.zip";
        private const string gitInstaller = "Git-2.21.0-64-bit.exe";
        private const string nodeInstaller = "node-v10.17.0-x64.msi";
        private const string nssmInstaller = "nssm.exe";
        private const string pythonInstaller = "python-2.7.16.amd64.msi";
        private const string buildToolsInstaller = "vs_buildtools__1482113758.1529499231.exe";
      
        private static readonly string progFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        private static readonly string rootFolder = Helper.GetConfigurationValue("InstallationFolder", Path.Combine( progFolder, "beame"));
        
        private static readonly string gitPath = Path.Combine(progFolder, "Git");
        private static readonly string gitCmdPath = Path.Combine(gitPath, "cmd");
        private static readonly string gitCmdExe = Path.Combine(gitCmdPath, "git.exe");
        
        private static readonly string nodeJSPath = Path.Combine(progFolder, "nodejs");
        private static readonly string npmPath = Path.Combine(nodeJSPath, "npm.cmd");
        private static readonly string nodePath = Path.Combine(nodeJSPath, "node.exe");
        
        private static readonly string pythonPath = Path.Combine(progFolder,"Python27");
        private static readonly string pythonFile = Path.Combine(pythonPath,"python.exe");

        private static readonly string nssmPath = Path.Combine(progFolder, "nssm");
        private static readonly string nssmFile = Path.Combine(nssmPath, nssmInstaller);
        
        private static readonly string registerSiteOnFinish = Helper.GetConfigurationValue("RegisterSiteOnFinish");
        private static readonly bool enableRegistrationTokenRequest = Helper.GetConfigurationValue("EnableRegistrationTokenRequest", false);
        private static readonly bool disableInstallDependencies = Helper.GetConfigurationValue("DisableInstallDependencies", false);
        
        private static readonly string[] installServiceAsAllowedFields = { "LocalSystem", "LocalService", "NetworkService", "User" };
        private static readonly string installServiceAs = Helper.GetConfigurationValue("InstallServiceAs", "NetworkService");
        
        private static readonly string proxyAddressProtocol = Helper.GetConfigurationValue("ProxyAddressProtocol");
        private static readonly string proxyAddressFqdn = Helper.GetConfigurationValue("ProxyAddressFqdn");
        private static readonly string proxyAddressPort = Helper.GetConfigurationValue("ProxyAddressPort");
        private static readonly string proxyAddressExcludes = Helper.GetConfigurationValue("ProxyAddressExcludes");
        private static readonly string externalOcspServerFqdn = Helper.GetConfigurationValue("ExternalOcspServerFqdn");
        private static readonly bool hasProxy = !string.IsNullOrWhiteSpace(proxyAddressFqdn);
        private static readonly string proxyAddress = hasProxy ? proxyAddressProtocol + "://" +  proxyAddressFqdn + (string.IsNullOrWhiteSpace(proxyAddressPort) ? "" : ":" + proxyAddressPort)
                                                               : "";
        

        private static readonly string versionToInstall = Helper.GetConfigurationValue("VersionToInstall", "latest");
        private static readonly string gatekeeperName = Helper.GetConfigurationValue("GatekeeperName", "Beame Gatekeeper");
        private static readonly string gatekeeperMode = Helper.GetConfigurationValue("GatekeeperMode", "Gatekeeper");
        private static readonly string logToFile = Helper.GetConfigurationValue("LogToFile", "true");
        private static readonly string logLevel = Helper.GetConfigurationValue("LogLevel", "INFO");
        
        private static readonly bool encryptUserData = Helper.GetConfigurationValue("EncryptUserData", true);
        private static readonly bool allowDirectSignin = Helper.GetConfigurationValue("AllowDirectSignin", true);
        private static readonly bool publicRegistration = Helper.GetConfigurationValue("PublicRegistration", false);
        private static readonly bool registrationImageRequired = Helper.GetConfigurationValue("RegistrationImageRequired", false);
        private static readonly bool allowSignInWithCreds = Helper.GetConfigurationValue("AllowSignInWithCreds", true);
        private static readonly bool allowSignInWithUltrasound = Helper.GetConfigurationValue("AllowSignInWithUltrasound", true);
        private static readonly bool disableDemoServers = Helper.GetConfigurationValue("DisableDemoServers", false);
        private static readonly bool advancedSettingsEnabled = Helper.GetConfigurationValue("AdvancedSettingsEnabled", false);
        private static readonly bool showZendeskSupport = Helper.GetConfigurationValue("ShowZendeskSupport", false);
        
        private static readonly string centralLoginUrl = Helper.GetConfigurationValue("CentralLoginUrl");
        private static readonly bool logoutToCentralLogin = Helper.GetConfigurationValue("LogoutToCentralLogin", false);

        private static readonly string customGatekeeper = Helper.GetConfigurationValue("CustomGatekeeper");
        private static readonly string customGatekeeperCSS = Helper.GetConfigurationValue("CustomGatekeeperCSS");

        enum InstallerOptions
        {
            Gatekeeper = 1,
            BeameSDK = 2,
            Dependencies = 4,
            Uninstall = 6,
            Exit = 9
        }
        enum SystemErrorCodes
        {
            ERROR_SUCCESS = 0,
            ERROR_INVALID_FUNCTION = 1,
            ERROR_FILE_NOT_FOUND = 2,
            ERROR_PATH_NOT_FOUND = 3,
            ERROR_ACCESS_DENIED = 5,
            ERROR_INVALID_HANDLE = 6,
            ERROR_NOT_ENOUGH_MEMORY = 8,
            ERROR_BAD_ENVIRONMENT = 10,
            ERROR_BAD_FORMAT = 11,
            ERROR_INVALID_ACCESS = 12,
            ERROR_INVALID_DATA = 13,
            ERROR_INSTALL_SERVICE_FAILURE = 1601,
            ERROR_INSTALL_USEREXIT = 1602,
            ERROR_INSTALL_FAILURE = 1603,
            ERROR_INSTALL_SUSPEND = 1604
        }
        
        

        static void Main(string[] args)
        {
            var interactive = args.Length == 0;

            Console.WriteLine("Beame.io Windows Installer");
            Console.WriteLine("**************************");
            Console.WriteLine();
            
            Console.WriteLine("->  interactive mode: " + interactive);
            Console.WriteLine("->  request registration token: " + enableRegistrationTokenRequest);
            if(!installServiceAsAllowedFields.Contains(installServiceAs))
                Exit("InstallServiceAs value '" + installServiceAs + "' is not allowed. Please use one of the following values: " + string.Join(", ", installServiceAsAllowedFields), interactive, SystemErrorCodes.ERROR_INVALID_DATA);
            Console.WriteLine("->  install service as: " + installServiceAs);
            Console.WriteLine("->  install dependencies: " + !disableInstallDependencies);
            if(!string.IsNullOrWhiteSpace(customGatekeeper))
                Console.WriteLine("->  install with custom gatekeeper: " + customGatekeeper);
            if(!string.IsNullOrWhiteSpace(customGatekeeperCSS))
                Console.WriteLine("->  install with custom gatekeeper: " + customGatekeeperCSS);
            
            if(!enableRegistrationTokenRequest)
                Console.WriteLine("->  show registration page on finish: " + registerSiteOnFinish);
            
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                Exit("Please run the installer with administrative rights", interactive, SystemErrorCodes.ERROR_ACCESS_DENIED);

            Console.WriteLine("->  administrator rights: True");

            var preChecks = PreChecks();
            if(preChecks != 0) 
                Exit("Pre-Checks failed!", interactive, preChecks);

            Console.WriteLine("->  pre-checks: Success");
            Console.WriteLine("->  installation folder: " + rootFolder);
            Console.WriteLine("->  third-party installation folder: " + progFolder);
            Console.WriteLine();
            
            string selected;
            if (interactive)
            {
                Console.WriteLine();
                Console.WriteLine("Available Options:");
                foreach (InstallerOptions option in Enum.GetValues(typeof(InstallerOptions)))
                {
                    Console.WriteLine("{0}. {1}", (int) option, option);
                }

                Console.WriteLine();
                Console.WriteLine("Please enter option number:");
                selected = Console.ReadLine();
                Console.WriteLine();
            }
            else
            {
                selected = args[0];
            }
            Enum.TryParse(selected, out InstallerOptions opt);
            Directory.CreateDirectory(rootFolder);
            
            var token = args.Length > 1 && enableRegistrationTokenRequest ? args[1] : "";
            if (enableRegistrationTokenRequest && (opt == InstallerOptions.Gatekeeper || opt == InstallerOptions.BeameSDK))
                token = requestRegistrationToken(token);
            
            SetupProxy();
            var env = SetupEnvVariables();
            var result = false;
            switch(opt)
            {
                case InstallerOptions.Gatekeeper:
                    result = (disableInstallDependencies || InstallDeps(env)) && InstallBeameGateKeeper(token, env);
                    break;
                
                case InstallerOptions.BeameSDK:
                    result =  (disableInstallDependencies || InstallDeps(env)) && InstallBeameSDK(token, env);
                    break;
    
                case InstallerOptions.Dependencies:
                    result = InstallDeps(env);
                    break;
                case InstallerOptions.Uninstall:
                    Console.WriteLine();
                    Console.Write("This is still a beta feature, not all components and settings will be removed");
                    result = Uninstall();
                    break;
                default:
                    Exit("No action required, exiting...", interactive);
                    break;
            }

            Console.WriteLine();
            var action = opt == InstallerOptions.Uninstall ? "Uninstaller" : "Installer";
            if (result)
            {
                if (!string.IsNullOrWhiteSpace(registerSiteOnFinish) && !enableRegistrationTokenRequest &&
                    (opt == InstallerOptions.Gatekeeper || opt == InstallerOptions.BeameSDK))
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

                if (opt == InstallerOptions.Gatekeeper && !enableRegistrationTokenRequest)
                {
                    Console.WriteLine();
                    Console.WriteLine("Once the credentials are installed, start the windows service '" +
                                      gatekeeperName + "' or by running 'sc.exe start \"" + gatekeeperName + "\"'");
                }
                
                Exit("== " + action + " finished successfully ==", interactive);
            }
            else
            {
                Exit("== " + action + " failed ==", interactive, SystemErrorCodes.ERROR_INSTALL_FAILURE);
            }
            
        }

        private static string requestRegistrationToken(string token)
        {
            var validToken = false;

            while (!validToken)
            {
                while (string.IsNullOrWhiteSpace(token))
                {
                    // extend readline size
                    Console.SetIn(new StreamReader(Console.OpenStandardInput(2048), Console.InputEncoding, false, 2048));
                    
                    Console.WriteLine();
                    Console.WriteLine("Please enter registration token:");
                    token = Console.ReadLine()?.Trim();
                    Console.WriteLine();
                }

                try
                {
                    var data = System.Convert.FromBase64String(token);
                    var base64Decoded = System.Text.Encoding.ASCII.GetString(data);
                    validToken = base64Decoded.Contains("authToken");
                    Console.WriteLine("decoded token is: " + base64Decoded);
                }
                catch
                {
                    Console.WriteLine("Inserted token is not valid...");
                    token = "";    
                }
            }

            return token;
        }

        private static void Exit(string message, bool interactive, SystemErrorCodes exitCode = SystemErrorCodes.ERROR_SUCCESS)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(message);
            Console.WriteLine();                    
            if (interactive)
            {
                Console.WriteLine("Nothing else to do, you can now close the application window");
                Console.ReadLine();
            }
            Environment.Exit((int)exitCode);
        }

        private static SystemErrorCodes PreChecks()
        {
            if (!string.IsNullOrWhiteSpace(customGatekeeper) && !File.Exists(customGatekeeper))
            {
                Console.WriteLine("Custom Gatekeeper file defined as '" + customGatekeeper + "' but not present in filesystem");
                return SystemErrorCodes.ERROR_FILE_NOT_FOUND;
            }
            if (!string.IsNullOrWhiteSpace(customGatekeeperCSS) && !File.Exists(customGatekeeperCSS))
            {
                Console.WriteLine("Custom Gatekeeper CSS file defined as '" + customGatekeeperCSS + "' but not present in filesystem");
                return SystemErrorCodes.ERROR_FILE_NOT_FOUND;
            }
             
            // TODO add more
            
            
            return SystemErrorCodes.ERROR_SUCCESS;
        }

        private static void SetupProxy()
        {
            var displayText = hasProxy ? "Setting" : "Removing";
            var fqdnWPort = proxyAddressFqdn + (string.IsNullOrWhiteSpace(proxyAddressPort) ? "" : ":" + proxyAddressPort); 
            
            if (Directory.Exists(gitPath) && File.Exists(gitCmdExe))
            {
                Console.WriteLine("--> " + displayText + " git proxy");
                Helper.StartAndCheckReturn(gitCmdExe, "config --global " + (hasProxy ? "http.proxy " + proxyAddress : "--unset http.proxy") );
                Helper.StartAndCheckReturn(gitCmdExe, "config --global "+ (hasProxy ? "https.proxy " + proxyAddress : "--unset https.proxy") );
            }

            if (File.Exists(npmPath))
            {
                Console.WriteLine("--> " + displayText + " npm proxy");
                Helper.StartAndCheckReturn(npmPath, "config " + (hasProxy ? "set proxy " + proxyAddress : "rm proxy") );
                Helper.StartAndCheckReturn(npmPath, "config " + (hasProxy ? "set https-proxy " + proxyAddress : "rm https-proxy") );
            }

            Console.WriteLine("--> " + displayText + " system proxy");
            var registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
            registry?.SetValue("ProxyEnable", hasProxy ? 1 : 0);
            registry?.SetValue("ProxyServer", hasProxy ? fqdnWPort : "");
            registry?.SetValue("ProxyOverride", hasProxy ? proxyAddressExcludes : "");
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

            Console.WriteLine("--> " + displayText + " cmdline proxy");
            Helper.SetEnv("HTTP_PROXY", hasProxy ? proxyAddress : "");
            Helper.SetEnv("HTTPS_PROXY", hasProxy ? proxyAddress : "");
            Helper.SetEnv("FTP_PROXY", hasProxy ? proxyAddress : "");
            Helper.SetEnv("NO_PROXY", hasProxy ? proxyAddressExcludes : "");
        }

        private static Dictionary<string, string> SetupEnvVariables()
        {
            Console.WriteLine("--> Setting environment variables");
            var env = new Dictionary<string, string>
            {
                {"BEAME_DIR", Path.Combine(rootFolder, ".beame")},
                {"BEAME_GATEKEEPER_DIR", rootFolder},
                {"BEAME_CDR_DIR", Path.Combine(rootFolder, ".beame_cdr")},
                {"BEAME_DATA_FOLDER", ".beame_data"},
                {"BEAME_SERVER_FOLDER", ".beame_server"},
                {"BEAME_LOG_TO_FILE", logToFile},
                {"BEAME_LOG_LEVEL", logLevel},
                {"NPM_CONFIG_PREFIX", rootFolder}
            };
            Helper.SetEnv(env);
            
            return env;
        }

        private static bool InstallBeameGateKeeper(string token, Dictionary<string, string> env)
        {
            var result = false;
            if (Helper.DoesServiceExist(gatekeeperName))
            {
                Console.WriteLine("--> Removing windows service");
                Helper.StopService(gatekeeperName);
                Helper.StartAndCheckReturn(nssmFile, "remove \"" + gatekeeperName + "\" confirm");
            }

            var gatekeeperPath = Path.Combine(rootFolder, @"node_modules\beame-gatekeeper");
            Console.WriteLine("--> Installing Beame.io Gatekeeper from " + (string.IsNullOrWhiteSpace(customGatekeeper) ? " version " + versionToInstall : customGatekeeper));
            try
            {
                //add GIT to path before starting this installation, in case GIT was just recently installed
                result = Helper.StartAndCheckReturn(npmPath, "install -g " + (string.IsNullOrWhiteSpace(customGatekeeper) ? "beame-gatekeeper@"+versionToInstall : customGatekeeper), @"C:\Program Files\Git\cmd", "", env);
                Console.WriteLine("Beame.io Gatekeeper installation " + (result ? "succeeded" : "failed"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Beame.io Gatekeeper installation failed - {0}", ex.Message);
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

                Console.WriteLine("--> Installing custom css Beame.io Gatekeeper");
                // Make install and gulp if any custom was applied
                result = result && 
                         Helper.StartAndCheckReturn(nodePath, @"node_modules\gulp\bin\gulp.js default", gitCmdPath, gatekeeperPath, env);
                Console.WriteLine("custom css Beame.io Gatekeeper installation " + (result ? "succeeded" : "failed"));
            }

            if (!result) return false;

            Console.WriteLine("--> Setting gatekeeper settings");
            result = ChangeGatekeeperSettings(env);
            Console.WriteLine("setting gatekeeper settings " + (result ? "succeeded" : "failed"));
            
            Console.WriteLine("--> creating windows service");
            result = result && Helper.StartAndCheckReturn(nssmFile, "install \"" + gatekeeperName + "\" \"" + Path.Combine(rootFolder, @"beame-gatekeeper.cmd") + "\" server start");
            Console.WriteLine("creating windows service " + (result ? "succeeded" : "failed"));
            
            
            Console.WriteLine("--> setting windows service preferences");

            if (installServiceAs.Equals("User"))
            {
                Console.WriteLine("Please insert current user " +  WindowsIdentity.GetCurrent().Name + " password:");
                var password = Console.ReadLine();
                result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" ObjectName " + WindowsIdentity.GetCurrent().Name + " " + password);
            }
            else
            {
                result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" ObjectName " + installServiceAs);
            }

            result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" AppDirectory \"" + rootFolder + "\"");
            result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" Start SERVICE_AUTO_START");
            result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" Description \"Beame Gatekeeper service\"");
            Console.WriteLine("setting windows service preferences " + (result ? "succeeded" : "failed"));
            
            // set folder permissions
            Helper.SetFolderAccessPermission(rootFolder, installServiceAs);
           
            // automatic register
            if (enableRegistrationTokenRequest)
            {
                Console.WriteLine("--> Registering token");
                result = result && Helper.StartAndCheckReturn(Path.Combine(rootFolder, @"beame-gatekeeper.cmd"),
                    "creds getCreds --regToken '" + token + "'",
                    Path.Combine(progFolder, "nodejs"), "", env);
                Console.WriteLine("registering token " + (result ? "succeeded" : "failed"));
            }

            Console.WriteLine("--> Starting service");
            Helper.StartAndCheckReturn(nssmFile, "start \"" + gatekeeperName + "\"");
            
            return result;
        }

        private static bool ChangeGatekeeperSettings(Dictionary<string, string> env = null)
        {
            var path = Path.Combine(rootFolder, @".beame_server\config\");
            var file = Path.Combine(path, "app_config.json");

            // if configs dont exist, initialize them
            if (!Directory.Exists(path))
            {
                Helper.StartAndCheckReturn(Path.Combine(rootFolder, @"beame-gatekeeper.cmd"), "server config", Path.Combine(progFolder, "nodejs"), "", env, 20);
            }

            if (!File.Exists(file)) return false;

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
            jsonObj["AdvancedSettingsEnabled"] = advancedSettingsEnabled;
            jsonObj["ShowZendeskSupport"] = showZendeskSupport;

            if (!string.IsNullOrWhiteSpace(centralLoginUrl))
                jsonObj["CentralLoginUrl"] = centralLoginUrl;
            jsonObj["LogoutToCentralLogin"] = logoutToCentralLogin;

            // proxy
            jsonObj["ProxySettings"]["host"] = hasProxy ? proxyAddressFqdn : "";
            jsonObj["ProxySettings"]["port"] = hasProxy ? proxyAddressPort : "";
            jsonObj["ProxySettings"]["excludes"] = hasProxy ? proxyAddressExcludes : "";
            jsonObj["ExternalOcspServerFqdn"] = hasProxy ? externalOcspServerFqdn : "";
        
            var output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(file, output);

            return true;
        }

        private static bool InstallBeameSDK(string token, Dictionary<string, string> env)
        {
            var result = false;
            Console.WriteLine("Installing Beame.io SDK...");

            try
            {
                //add GIT to path before starting this installation, in case GIT was just recently installed
                result = Helper.StartAndCheckReturn(npmPath, "install -g beame-sdk@"+versionToInstall, gitCmdPath, "", env);
                Console.WriteLine("Beame.io SDK installation " + (result ? "succeeded" : "failed"));
                
                // automatic register
                if (enableRegistrationTokenRequest) 
                {
                    Console.WriteLine("--> Registering token");
                    Helper.StartAndCheckReturn(Path.Combine(rootFolder, @"beame.cmd"), "creds getCreds --regToken '" + token + "' --authSrvFqdn ypxf72akb6onjvrq.ohkv8odznwh5jpwm.v1.p.beameio.net",
                        Path.Combine(progFolder, "nodejs"), "", env);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Beame.io SDK installation failed - {0}", ex.Message);
            }

            return result;
        }
        
        #region dependencies
        private static bool InstallDeps(Dictionary<string, string> env)
        {
            if (!Directory.Exists(Path.GetTempPath())) 
                Directory.CreateDirectory(Path.GetTempPath());
            return InstallNSSM() && InstallOpenSSL() && InstallGit() && InstallPython() && InstallBuildTools() &&
                   InstallNode(env);
        }
        
        private static bool InstallGit()
        {
            Console.WriteLine("Installing Git...");
            var result = true;

            //check for GIT and install it if necessary
            if (!Directory.Exists(gitPath) || !File.Exists(gitCmdExe))
            {
                string exePath = Path.Combine(Path.GetTempPath(), gitInstaller);
                Helper.WriteResourceToFile(gitInstaller, exePath);

                result = Helper.StartAndCheckReturn(exePath, "/VERYSILENT /CLOSEAPPLICATIONS /NORESTART");
                Helper.AddToPath(Path.Combine(gitPath, @"cmd"));
                Console.WriteLine("Git installation " + (result ? "succeeded" : "failed"));
            }
            else
            {
                Console.WriteLine("Git already installed");
            }

            // set git proxy if defined
            if (!string.IsNullOrWhiteSpace(proxyAddress))
            {
                Helper.StartAndCheckReturn(gitCmdExe, "config --global http.proxy " + proxyAddress);
                Helper.StartAndCheckReturn(gitCmdExe, "config --global https.proxy " + proxyAddress);
            }
            return result;
        }

        private static bool InstallNSSM()
        {
            Console.WriteLine("Installing NSSM...");
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

            return true;
        }

        private static bool InstallPython()
        {
            Console.WriteLine("Installing Python...");
            var result = true;
            var msiPath = Path.Combine(Path.GetTempPath(), pythonInstaller);            
            try
            {
                //check if python.exe exist
                if (File.Exists(pythonFile))
                {
                    Console.WriteLine("Already installed...");
                }
                else
                {
                    Helper.WriteResourceToFile(pythonInstaller, msiPath);
                    result = Helper.StartAndCheckReturn("msiexec", "/i " + msiPath + " TARGETDIR=\""+ pythonPath +"\" ALLUSERS=0 /qn");
                   
                    Console.WriteLine("Python installation " + (result ? "succeeded" : "failed"));

                    Helper.AddToPath(pythonPath);
                    Helper.SetEnv("PYTHON", pythonFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Python installation error - {0}", ex.Message);
                result = false;
            }
            finally
            {
                Helper.RemoveFile(msiPath);
            }

            return result;
        }
        private static bool InstallBuildTools()
        {
            Console.WriteLine("Installing Microsoft Build Tools... This can take a while, please wait...");
            var result = true;
            var exePath = Path.Combine(Path.GetTempPath(), buildToolsInstaller);
            
            try
            { 
                Helper.WriteResourceToFile(buildToolsInstaller, exePath);
                // options available here: https://docs.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-build-tools?view=vs-2017
                var parameters = "--add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.Windows81SDK --add Microsoft.VisualStudio.Component.VC.140 --includeRecommended --passive --wait";
                result = Helper.StartAndCheckReturn(exePath, parameters, "", "", null, 3200)
                         // if installation fails, try update
                         || Helper.StartAndCheckReturn(exePath, "update " + parameters, "", "", null, 3200);

                Helper.SetEnv("GYP_MSVS_VERSION", "2017");
                Console.WriteLine("Microsoft Build Tools installation " + (result ? "succeeded" : "failed"));
                if(!result) Console.WriteLine("  -- Please restart the computer and try running the setup again --");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Microsoft Build Tools installation error - {0}", ex.Message);
                result = false;
            }
            finally
            {
                Helper.RemoveFile(exePath);
            }

            return result;
        }
        

        private static bool InstallNode(Dictionary<string, string> env)
        {
            Console.WriteLine("Installing NodeJS...");
            var result = true;
            var msiPath = Path.Combine(Path.GetTempPath(), "nodeJS.msi");

            try
            {
                //check if NPM and node.exe exist
                if (File.Exists(npmPath) && File.Exists(Path.Combine(nodeJSPath, "node.exe")))
                {
                    Console.WriteLine("Already installed...");
                }
                else
                {
                    Helper.WriteResourceToFile(nodeInstaller, msiPath);
                    result = Helper.StartAndCheckReturn("msiexec", "/i " + msiPath + " /quiet /qn /norestart");
                    Console.WriteLine("NodeJS installation " + (result ? "succeeded" : "failed"));

                    Helper.AddToPath(nodeJSPath);
                    Helper.AddToPath(rootFolder);
                    Helper.StartAndCheckReturn(npmPath, "config --global set prefix \"" + rootFolder + "\"");
                    Helper.StartAndCheckReturn(npmPath, "config --global set python \"" + pythonFile + "\"");
                    Helper.StartAndCheckReturn(npmPath, "config --global set msvs_version 2017");
                }

                if (result)
                {
                    // set npm proxy if defined
                    if (!string.IsNullOrWhiteSpace(proxyAddress))
                    {
                        Helper.StartAndCheckReturn(npmPath, "config set proxy " + proxyAddress);
                        Helper.StartAndCheckReturn(npmPath, "config set https-proxy " + proxyAddress);
                    }
                    
                    Console.WriteLine("Updating npm packages... This can take a while, please wait...");

                    env.Add("PYTHON", pythonFile);
                    //run NPM upgrade
                    result = Helper.StartAndCheckReturn(npmPath, "cache verify","", "", env) &&
                             Helper.StartAndCheckReturn(npmPath, "install -g npm@latest", "", "", env) &&
                             Helper.StartAndCheckReturn(npmPath, "install -g node-gyp", "", "", env);
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
                    var tmpPath = Path.Combine(Path.GetTempPath(), openSSLInstaller);
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

        private static bool Uninstall()
        {
            var result = true;

            try
            {
                if (File.Exists(npmPath))
                {
                    result = Helper.StartAndCheckReturn(npmPath, "uninstall -g beame-gatekeeper") &&
                             Helper.StartAndCheckReturn(npmPath, "uninstall -g node-gyp") &&
                             Helper.StartAndCheckReturn(npmPath, "uninstall -g beame-sdk");

                    Directory.Delete(Path.GetTempPath(),true);
                }
                
                if (Helper.DoesServiceExist(gatekeeperName))
                {
                    Console.WriteLine("--> Removing windows service");
                    Helper.StopService(gatekeeperName);
                    Helper.StartAndCheckReturn(nssmFile, "remove \"" + gatekeeperName + "\" confirm");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed - {0}", ex.Message);
                return false;
            }
            return result;
        }
    }
}