using System;
using System.IO;
using System.Security.Cryptography;

namespace Nethereum.DevChain.Hosting
{
    public static class EngineJwtSecret
    {
        public const int SecretLengthBytes = 32;

        public static byte[] LoadOrCreate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A jwt secret path is required", nameof(path));

            if (File.Exists(path))
            {
                var hex = File.ReadAllText(path).Trim();
                return Convert.FromHexString(hex);
            }

            var secret = RandomNumberGenerator.GetBytes(SecretLengthBytes);

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, Convert.ToHexString(secret).ToLowerInvariant());

            return secret;
        }
    }
}
