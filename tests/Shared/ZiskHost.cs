using System;
using System.Diagnostics;

namespace Nethereum.Testing
{
    public static class ZiskHost
    {
        private const string ZiskPath = "export PATH=$HOME/.zisk/bin:$HOME/.cargo/bin:$PATH";

        public static string Path(string path) =>
            OperatingSystem.IsWindows() ? ToWslPath(path) : path;

        public static ProcessStartInfo Command(string commandLine, bool redirectStandardError = false)
        {
            var script = $"{ZiskPath} && {commandLine}";
            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = redirectStandardError,
                CreateNoWindow = true
            };

            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "wsl";
                psi.Arguments = $"-d Ubuntu -e bash -c \"{script}\"";
            }
            else
            {
                psi.FileName = "bash";
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(script);
            }

            return psi;
        }

        private static string ToWslPath(string windowsPath) =>
            windowsPath.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("c:/", "/mnt/c/");
    }
}
