using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;

namespace BeameWindowsInstaller
{
    public static class Helper
    {
        public static void AddToPath(string path)
        {
            var varPath = GetEnv("Path");
            if (varPath.Contains(@path)) return;
            
            varPath += @";" + @path;
            SetEnv("Path", varPath);
        }

        public static string GetEnv(string name, EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine) 
        {
            return Environment.GetEnvironmentVariable(name, target);
        }
        public static void SetEnv(string name, string value, EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine) 
        {
            Environment.SetEnvironmentVariable(name, value, target);
        }

        public static void SetEnv(Dictionary<string, string> addToEnv, EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine)
        {
            if (addToEnv == null) return;
            foreach (var env in addToEnv)
            {
                SetEnv(env.Key, env.Value, target);
            }
        }
        
        public static void WriteResourceToFile(string resourceName, string fileName)
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("BeameWindowsInstaller.Resources." + resourceName))
            {
                using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    resource?.CopyTo(file);
                }
            }
        }

        public static bool StartAndCheckReturn(string fileName, string arguments, string addToPath = "", string workingDir = "", Dictionary<string,string> addToEnv = null, int timeoutSeconds = 1200)
        {
            try
            {
                var procStartInfo = new ProcessStartInfo()
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false
                };

                if (!string.IsNullOrWhiteSpace(workingDir))
                    procStartInfo.WorkingDirectory = workingDir;

                if (!string.IsNullOrWhiteSpace(addToPath))
                {
                    var envPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                    envPath += ";" + addToPath;
                    procStartInfo.EnvironmentVariables["PATH"] = envPath;
                }

                if (addToEnv != null)
                {
                    foreach (var env in addToEnv)
                    {
                        procStartInfo.EnvironmentVariables[env.Key] = env.Value;
                    }
                }

                var proc = Process.Start(procStartInfo);
                if (proc == null) return false;

                var timedOut = !proc.WaitForExit(timeoutSeconds * 1000);
                if (proc.ExitCode < 0)
                {
                    Environment.ExitCode = proc.ExitCode;
                }

                return !timedOut && proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
        
        public static void RemoveFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        }

        public static string GetConfigurationValue(string property, string defvalue = "")
        {
            if (ConfigurationManager.AppSettings.AllKeys.Any(x => x.Equals(property)))
            {
                try
                {
                    return ConfigurationManager.AppSettings[property];
                }
                catch { }
            }
            return  defvalue;
        }
        
        public static bool GetConfigurationValue(string property, bool defvalue)
        {
            if (ConfigurationManager.AppSettings.AllKeys.Any(x => x.Equals(property)))
            {
                try
                {
                    return bool.Parse(ConfigurationManager.AppSettings[property]);
                }
                catch{}
            }
            return  defvalue;
        }
        
        public static bool DoesServiceExist(string serviceName, string machineName = "localhost")
        {
            var services = ServiceController.GetServices(machineName);
            return services.Any(s => s.ServiceName.Equals(serviceName));
        }
        
        public static void StopService(string serviceName, string machineName = "localhost")
        {
            var services = ServiceController.GetServices(machineName);
            services.FirstOrDefault(s => s.ServiceName.Equals(serviceName) && s.Status != ServiceControllerStatus.Stopped)?.Stop();
        }
        
        public static void SetFolderAccessPermission(string directoryPath,string username)
        {
            var dirSecurity = Directory.GetAccessControl(directoryPath);

            //remove any inherited access
            dirSecurity.SetAccessRuleProtection(true, false);
            
            //get and remove any special user access
            var rules = dirSecurity.GetAccessRules(true, true, typeof(NTAccount));
            foreach (FileSystemAccessRule rule in rules)
                dirSecurity.RemoveAccessRule(rule);
           
            // add new access rules
            if (username != WindowsIdentity.GetCurrent().Name)
            {
                dirSecurity.AddAccessRule(new FileSystemAccessRule(username,
                                                                FileSystemRights.FullControl,
                                                                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                                PropagationFlags.None, 
                                                                AccessControlType.Allow));
            }

            dirSecurity.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name,
                                                                FileSystemRights.FullControl,
                                                                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                                PropagationFlags.None, 
                                                                AccessControlType.Allow));
            
            var ownerAccount = new NTAccount(WindowsIdentity.GetCurrent().Name);
            dirSecurity.SetOwner(ownerAccount);

            Directory.SetAccessControl(directoryPath, dirSecurity);
        }
    }
}