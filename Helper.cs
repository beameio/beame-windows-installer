using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BeameWindowsInstaller
{
    public static class Helper
    {
        public static void AddToPath(string path)
        {
            var varPath = GetEnv("Path");
            if (varPath.Contains(path)) return;
            
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

        public static bool StartAndCheckReturn(string fileName, string arguments, bool useShellExecute = false, string addToPath = "", int timeoutSeconds = 600, string workingDir = "")
        {
            try
            {
                var procStartInfo = new ProcessStartInfo()
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = useShellExecute,
                };

                if (!string.IsNullOrWhiteSpace(workingDir))
                    procStartInfo.WorkingDirectory = workingDir;

                if (!string.IsNullOrWhiteSpace(addToPath))
                {
                    string envPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                    envPath += ";" + addToPath;
                    procStartInfo.EnvironmentVariables["PATH"] = envPath;
                }

                var proc = Process.Start(procStartInfo);
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
            catch
            { }
        }

        public static string GetConfigurationValue(string property, string defvalue = "")
        {
            if (ConfigurationManager.AppSettings.AllKeys.Any(x => x.Equals(property)))
            {
                try
                {
                    return ConfigurationManager.AppSettings[property];
                }
                catch{}
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
    }
}