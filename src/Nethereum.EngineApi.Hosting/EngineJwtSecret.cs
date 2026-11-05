using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nethereum.EngineApi.Hosting
{
    public static class EngineJwtSecret
    {
        public const int SecretLengthBytes = 32;

        public static byte[] LoadOrCreate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A jwt secret path is required", nameof(path));

            if (File.Exists(path))
                return Parse(path);

            var secret = RandomNumberGenerator.GetBytes(SecretLengthBytes);
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                WriteOwnerOnly(temp, Convert.ToHexString(secret).ToLowerInvariant());
                File.Move(temp, fullPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(fullPath))
            {
                return Parse(path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }

            return secret;
        }

        private static void WriteOwnerOnly(string path, string content)
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            using var stream = new FileStream(path, options);
            var bytes = Encoding.ASCII.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] Parse(string path)
        {
            var hex = File.ReadAllText(path).Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);

            byte[] secret;
            try
            {
                secret = Convert.FromHexString(hex);
            }
            catch (FormatException)
            {
                throw new InvalidDataException(
                    $"The jwt secret file '{path}' is not hex; it must hold {SecretLengthBytes} bytes as hex, optionally prefixed with 0x");
            }

            if (secret.Length != SecretLengthBytes)
                throw new InvalidDataException(
                    $"The jwt secret file '{path}' holds {secret.Length} bytes; it must hold exactly {SecretLengthBytes}");

            return secret;
        }
    }
}
