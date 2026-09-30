using System;
using System.IO;

namespace Nethereum.DevP2P.NodeDb
{
    internal static class AtomicFile
    {
        public static void WriteAllBytes(string path, byte[] bytes, UnixFileMode? unixMode = null)
        {
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            Finish(tmp, path, unixMode);
        }

        public static void WriteAllText(string path, string text, UnixFileMode? unixMode = null)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            Finish(tmp, path, unixMode);
        }

        private static void Finish(string tmp, string path, UnixFileMode? unixMode)
        {
            if (unixMode.HasValue && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmp, unixMode.Value);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
