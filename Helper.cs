using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace BeameWindowsInstaller
{
    public static class Helper
    {
        public static void SetEnv(string name, string value) 
        {
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Machine);
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

        public static bool StartAndCheckReturn(string fileName, string arguments, bool useShellExecute = false, string addToPath = "", int timeoutMinutes = 10, string workingDir = "")
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
            bool timedOut = !proc.WaitForExit(timeoutMinutes * 60 * 1000);

            if (proc.ExitCode < 0)
            {
                Environment.ExitCode = proc.ExitCode;
            }

            return !timedOut && proc.ExitCode == 0;
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
    }
}