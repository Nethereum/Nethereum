using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.Avalonia.Services
{
    public class AvaloniaWalletVaultService : WalletVaultServiceBase
    {
        private readonly string _vaultPath;

        public AvaloniaWalletVaultService()
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NethereumWallet");
            Directory.CreateDirectory(basePath);
            _vaultPath = Path.Combine(basePath, "vault.dat");
        }

        public override Task<bool> VaultExistsAsync()
        {
            return Task.FromResult(File.Exists(_vaultPath));
        }

        protected override Task<string?> GetEncryptedAsync()
        {
            if (!File.Exists(_vaultPath))
            {
                return Task.FromResult<string?>(null);
            }

            var payload = File.ReadAllBytes(_vaultPath);
            var decrypted = Unprotect(payload);
            return Task.FromResult<string?>(decrypted);
        }

        protected override Task SaveEncryptedAsync(string encrypted)
        {
            var payload = Protect(encrypted);
            File.WriteAllBytes(_vaultPath, payload);
            return Task.CompletedTask;
        }

        protected override Task ResetStorageAsync()
        {
            if (File.Exists(_vaultPath))
            {
                File.Delete(_vaultPath);
            }
            return Task.CompletedTask;
        }

        private byte[] Protect(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                using var aes = Aes.Create();
                aes.Key = GetFallbackKey();
                aes.IV = new byte[16];
                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(bytes, 0, bytes.Length);
                }
                return ms.ToArray();
            }

            return bytes;
        }

        private string Unprotect(byte[] payload)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var decrypted = ProtectedData.Unprotect(payload, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                using var aes = Aes.Create();
                aes.Key = GetFallbackKey();
                aes.IV = new byte[16];
                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(payload);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var result = new MemoryStream();
                cs.CopyTo(result);
                return Encoding.UTF8.GetString(result.ToArray());
            }

            return Encoding.UTF8.GetString(payload);
        }

        private byte[] GetFallbackKey()
        {
            using var sha = SHA256.Create();
            var seed = Environment.MachineName + Environment.UserName;
            return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }
    }
}
