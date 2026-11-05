using System;
using System.IO;
using System.Text;

namespace Nethereum.DevP2P.NodeDb
{
    internal static class AtomicFile
    {
        public static void WriteAllBytes(string path, byte[] bytes, UnixFileMode? unixMode = null)
        {
            var tmp = path + ".tmp";
            WriteTemp(tmp, bytes, unixMode);
            Finish(tmp, path);
        }

        public static void WriteAllText(string path, string text, UnixFileMode? unixMode = null)
        {
            var tmp = path + ".tmp";
            WriteTemp(tmp, new UTF8Encoding(false).GetBytes(text), unixMode);
            Finish(tmp, path);
        }

        private static void WriteTemp(string tmp, byte[] bytes, UnixFileMode? unixMode)
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (unixMode.HasValue && !OperatingSystem.IsWindows())
                options.UnixCreateMode = unixMode.Value;

            File.Delete(tmp);
            using var stream = new FileStream(tmp, options);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void Finish(string tmp, string path)
        {
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
