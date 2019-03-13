using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        private const string pythonInstaller = "python-2.7.15.amd64.msi";
        private const string buildToolsInstaller = "vs_buildtools__1482113758.1529499231.exe";
      
        private static readonly string progFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        private static readonly string nodeJSPath = Path.Combine(progFolder, "nodejs");
        private static readonly string npmPath = Path.Combine(nodeJSPath, "npm.cmd");
        private static readonly string pythonPath = Path.Combine(progFolder,"Python27");
        private static readonly string pythonFile = Path.Combine(pythonPath,"python.exe");

        private static readonly string nssmPath = Path.Combine(progFolder, "nssm");
        private static readonly string nssmFile = Path.Combine(nssmPath, nssmInstaller);
        
        private static readonly string registerSiteOnFinish = Helper.GetConfigurationValue("RegisterSiteOnFinish");
        private static readonly bool enableRegistrationTokenRequest = Helper.GetConfigurationValue("EnableRegistrationTokenRequest", false);
        private static readonly bool disableInstallDependencies = Helper.GetConfigurationValue("DisableInstallDependencies", false);
        
        private static readonly string installServiceAs = Helper.GetConfigurationValue("InstallServiceAs", "NetworkService");
        
        private static readonly string proxyAddressProtocol = Helper.GetConfigurationValue("ProxyAddressProtocol");
        private static readonly string proxyAddressFqdn = Helper.GetConfigurationValue("ProxyAddressFqdn");
        private static readonly string proxyAddressPort = Helper.GetConfigurationValue("ProxyAddressPort");
        private static readonly string proxyAddressExcludes = Helper.GetConfigurationValue("ProxyAddressExcludes");
        private static readonly string externalOcspServerFqdn = Helper.GetConfigurationValue("ExternalOcspServerFqdn");
        private static readonly string proxyAddress = string.IsNullOrWhiteSpace(proxyAddressFqdn) 
            ? "" 
            : proxyAddressProtocol + "://" +  proxyAddressFqdn + (string.IsNullOrWhiteSpace(proxyAddressPort) ? "" : ":" + proxyAddressPort);

        private static readonly string versionToInstall = Helper.GetConfigurationValue("VersionToInstall", "latest");
        private static readonly string gatekeeperName = Helper.GetConfigurationValue("GatekeeperName", "Beame Gatekeeper");
        private static readonly string gatekeeperMode = Helper.GetConfigurationValue("GatekeeperMode", "Gatekeeper");
        private static readonly bool encryptUserData = Helper.GetConfigurationValue("EncryptUserData", true);
        private static readonly bool allowDirectSignin = Helper.GetConfigurationValue("AllowDirectSignin", true);
        private static readonly bool publicRegistration = Helper.GetConfigurationValue("PublicRegistration", false);
        private static readonly bool registrationImageRequired = Helper.GetConfigurationValue("RegistrationImageRequired", false);
        private static readonly bool allowSignInWithCreds = Helper.GetConfigurationValue("AllowSignInWithCreds", true);
        private static readonly bool allowSignInWithUltrasound = Helper.GetConfigurationValue("AllowSignInWithUltrasound", true);
        private static readonly bool disableDemoServers = Helper.GetConfigurationValue("DisableDemoServers", false);
        private static readonly bool advancedSettingsEnabled = Helper.GetConfigurationValue("AdvancedSettingsEnabled", false);
        private static readonly bool showZendeskSupport = Helper.GetConfigurationValue("ShowZendeskSupport", false);

        private static readonly string customGatekeeper = Helper.GetConfigurationValue("CustomGatekeeper");
        private static readonly string customGatekeeperCSS = Helper.GetConfigurationValue("CustomGatekeeperCSS");

        private static string rootFolder;

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


            var installationFolder = Helper.GetConfigurationValue("InstallationFolder");
            rootFolder = string.IsNullOrWhiteSpace(installationFolder) ? Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm") : installationFolder; 
            
            var gkenv = new Dictionary<string, string>
            {
                {"BEAME_DIR", Path.Combine(rootFolder, ".beame")},
                {"BEAME_GATEKEEPER_DIR", rootFolder},
                {"BEAME_CDR_DIR", Path.Combine(rootFolder, ".beame_cdr")},
                {"BEAME_DATA_FOLDER", ".beame_data"},
                {"BEAME_SERVER_FOLDER", ".beame_server"}
            };

            if (!string.IsNullOrWhiteSpace(installationFolder))
            {
                Directory.CreateDirectory(rootFolder);
                Helper.SetEnv("NPM_CONFIG_PREFIX", rootFolder);
                Helper.SetEnv(gkenv);
            }

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
            var result = false;
            var token = args.Length > 1 && enableRegistrationTokenRequest ? args[1] : "";
          
            switch(opt)
            {
                case InstallerOptions.Gatekeeper:
                    if (enableRegistrationTokenRequest) token = requestRegistrationToken(token);
                    result = (disableInstallDependencies || InstallDeps()) && InstallBeameGateKeeper(token, gkenv);
                    break;
                
                case InstallerOptions.BeameSDK:
                    if (enableRegistrationTokenRequest) token = requestRegistrationToken(token);
                    result =  (disableInstallDependencies || InstallDeps()) && InstallBeameSDK(token, gkenv);
                    break;
    
                case InstallerOptions.Dependencies:
                    result = InstallDeps();
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
                    Console.SetIn(new StreamReader(Console.OpenStandardInput(2048)));
                    
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

        private static bool InstallBeameGateKeeper(string token, Dictionary<string, string> gkenv)
        {
            var result = false;

            var nodeJSPath = Path.Combine(progFolder, "nodejs");
            var npmPath = Path.Combine(nodeJSPath, "npm.cmd");
            var nodePath = Path.Combine(nodeJSPath, "node.exe");
            
            if (Helper.DoesServiceExist(gatekeeperName))
            {
                Console.WriteLine("--> removing windows service");
                Helper.StopService(gatekeeperName);
                Helper.StartAndCheckReturn(nssmFile, "remove \"" + gatekeeperName + "\" confirm");
            }

            var gatekeeperPath = Path.Combine(rootFolder, @"node_modules\beame-gatekeeper");
            Console.WriteLine("--> Installing Beame.io Gatekeeper from " + (string.IsNullOrWhiteSpace(customGatekeeper) ? " version " + versionToInstall : customGatekeeper));
            try
            {
                //add GIT to path before starting this installation, in case GIT was just recently installed
                result = Helper.StartAndCheckReturn(npmPath, "install -g " + (string.IsNullOrWhiteSpace(customGatekeeper) ? "beame-gatekeeper@"+versionToInstall : customGatekeeper), @"C:\Program Files\Git\cmd", "", gkenv);
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
                         Helper.StartAndCheckReturn(nodePath, @"node_modules\gulp\bin\gulp.js sass web_sass compile", @"C:\Program Files\Git\cmd", gatekeeperPath, gkenv);
                Console.WriteLine("custom css Beame.io Gatekeeper installation " + (result ? "succeeded" : "failed"));
            }

            if (!result) return false;

            Console.WriteLine("--> Setting gatekeeper settings");
            result = ChangeGatekeeperSettings(gkenv);
            Console.WriteLine("setting gatekeeper settings " + (result ? "succeeded" : "failed"));
            
            Console.WriteLine("--> creating windows service");
            result = result && Helper.StartAndCheckReturn(nssmFile, "install \"" + gatekeeperName + "\" \"" + Path.Combine(rootFolder, @"beame-gatekeeper.cmd") + "\" server start");
            Console.WriteLine("creating windows service " + (result ? "succeeded" : "failed"));
            
            
            Console.WriteLine("--> setting windows service preferences");
            var userName = installServiceAs.Equals("User") ? WindowsIdentity.GetCurrent().Name : installServiceAs;
            if (installServiceAs.Equals("User"))
            {
                Console.WriteLine("Please insert current user " +  userName + " password:");
                var password = Console.ReadLine();
                result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" ObjectName " + userName + " " + password);
            }
            else
            {
                result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" ObjectName " + userName);
            }

            result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" AppDirectory \"" + rootFolder + "\"");
            result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" Start SERVICE_AUTO_START");
            result = result && Helper.StartAndCheckReturn(nssmFile, "set \"" + gatekeeperName + "\" Description \"Beame Gatekeeper service\"");
            Console.WriteLine("setting windows service preferences " + (result ? "succeeded" : "failed"));
            
            // set folder permissions
            Helper.SetFolderAccessPermission(rootFolder, userName);
            
            // activate file logging on gatekeeper
            Helper.SetEnv("BEAME_LOG_TO_FILE", "true");
            
            // automatic register
            if (enableRegistrationTokenRequest)
            {
                Console.WriteLine("--> Registering token");
                result = result && Helper.StartAndCheckReturn(Path.Combine(rootFolder, @"beame-gatekeeper.cmd"),
                    "creds getCreds --regToken '" + token + "'",
                    Path.Combine(progFolder, "nodejs"), "", gkenv);
                Console.WriteLine("registering token " + (result ? "succeeded" : "failed"));

                Console.WriteLine("--> Starting service");
                result = result &&  Helper.StartAndCheckReturn(nssmFile, "start \"" + gatekeeperName + "\"");
                Console.WriteLine("service start " + (result ? "succeeded" : "failed"));
            }

            return result;
        }

        private static bool ChangeGatekeeperSettings(Dictionary<string, string> gkenv = null)
        {
            var path = Path.Combine(rootFolder, @".beame_server\config\");
            var file = Path.Combine(path, "app_config.json");

            // if configs dont exist, initialize them
            if (!Directory.Exists(path))
            {
                Helper.StartAndCheckReturn(Path.Combine(rootFolder, @"beame-gatekeeper.cmd"), "server config", Path.Combine(progFolder, "nodejs"), "", gkenv, 20);
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
            
            if (!string.IsNullOrWhiteSpace(proxyAddress))
            {
                jsonObj["ProxySettings"]["host"] = proxyAddressFqdn;
                jsonObj["ProxySettings"]["port"] = proxyAddressPort;
                jsonObj["ProxySettings"]["excludes"] = proxyAddressExcludes;
                jsonObj["ExternalOcspServerFqdn"] = externalOcspServerFqdn;
            }
            var output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(file, output);

            return true;
        }

        private static bool InstallBeameSDK(string token, Dictionary<string, string> gkenv)
        {
            var result = false;
            Console.WriteLine("Installing Beame.io SDK...");

            var nodeJSPath = Path.Combine(progFolder, "nodejs");
            var npmPath = Path.Combine(nodeJSPath, "npm.cmd");
            try
            {
                //add GIT to path before starting this installation, in case GIT was just recently installed
                result = Helper.StartAndCheckReturn(npmPath, "install -g beame-sdk@"+versionToInstall, @"C:\Program Files\Git\cmd", "", gkenv);
                Console.WriteLine("Beame.io SDK installation " + (result ? "succeeded" : "failed"));
                
                // automatic register
                if (enableRegistrationTokenRequest) 
                {
                    Console.WriteLine("--> Registering token");
                    Helper.StartAndCheckReturn(Path.Combine(rootFolder, @"beame.cmd"), "creds getCreds --regToken '" + token + "' --authSrvFqdn ypxf72akb6onjvrq.ohkv8odznwh5jpwm.v1.p.beameio.net",
                        Path.Combine(progFolder, "nodejs"), "", gkenv);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Beame.io SDK installation failed - {0}", ex.Message);
            }

            return result;
        }
        
        #region dependencies
        private static bool InstallDeps() {
            if (!InstallNSSM() || !InstallOpenSSL() || !InstallGit() || !InstallPython() || !InstallBuildTools() ||
                !InstallNode()) return false;

            // set env proxy
            if (!string.IsNullOrWhiteSpace(proxyAddress))
            {
                Console.WriteLine("--> Setting cmdline proxy");
                Helper.SetEnv("HTTP_PROXY", proxyAddress);
                Helper.SetEnv("HTTPS_PROXY", proxyAddress);
                Helper.SetEnv("FTP_PROXY", proxyAddress);
                Helper.SetEnv("NO_PROXY", proxyAddressExcludes);
            }

            return true;
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
                Console.WriteLine("Git installation " + (result ? "succeeded" : "failed"));
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
            var msiPath = Path.Combine(Path.GetTempPath(), "python-2.7.15.amd64.msi");            
            try
            {
                //check if python.exe exist
                if (File.Exists(pythonPath))
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
                var parameters = "--add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.Windows81SDK --add Microsoft.VisualStudio.Component.VC.140 --includeRecommended --norestart --noUpdateInstaller --passive --wait";
                result = Helper.StartAndCheckReturn(exePath, parameters, "", "", null, 3200)
                         // if installation fails, try update
                         || Helper.StartAndCheckReturn(exePath, "update " + parameters, "", "", null, 3200);

                Helper.SetEnv("GYP_MSVS_VERSION", "2017");
                Console.WriteLine("Microsoft Build Tools installation " + (result ? "succeeded" : "failed"));
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
        

        private static bool InstallNode()
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
                    Helper.StartAndCheckReturn(npmPath, "config --global set prefix " + rootFolder);
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

                    var nodeenv= new Dictionary<string, string>();
                    nodeenv.Add("NPM_CONFIG_PREFIX", rootFolder);
                    nodeenv.Add("PYTHON", pythonFile);
                    //run NPM upgrade
                    result = Helper.StartAndCheckReturn(npmPath, "install -g npm@latest", "", "", nodeenv) &&
                             Helper.StartAndCheckReturn(npmPath, "install -g node-gyp", "", "", nodeenv);
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
                             Helper.StartAndCheckReturn(npmPath, "uninstall -g node-gyp");

                    Directory.Delete(Path.GetTempPath(),true);
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