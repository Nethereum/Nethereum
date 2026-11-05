using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Wallet.WalletAccounts;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9VaultIntegrityFlagTests : IDisposable
    {
        private const string Password = "vault password";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private const string RealAddress = "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b";
        private const string WrongAddress = "0x2222222222222222222222222222222222222222";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "s9flag-" + Guid.NewGuid().ToString("N"));

        public S9VaultIntegrityFlagTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        private sealed class IdentityStrategy : IEncryptionStrategy
        {
            public byte[] Encrypt(byte[] data, string password) => data;
            public byte[] Decrypt(byte[] data, string password) => data;
        }

        private static byte[] Plain(string address)
        {
            var vault = new WalletVault(new IdentityStrategy());
            vault.Accounts.Add(new PrivateKeyWalletAccount(address, "cold", PrivateKeyHex));
            return Convert.FromBase64String(vault.Encrypt(Password));
        }

        private static byte[] LegacyCbc(byte[] plain, HashAlgorithmName hash)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateIV();
            aes.Key = new Rfc2898DeriveBytes(Password, Encoding.UTF8.GetBytes("NethereumWallet16"), 10000, hash).GetBytes(32);
            using var encryptor = aes.CreateEncryptor();
            return aes.IV.Concat(encryptor.TransformFinalBlock(plain, 0, plain.Length)).ToArray();
        }

        private static byte[] ReadBlob(string path) => Convert.FromBase64String(File.ReadAllText(path));

        [Trait("Rule", "S9-R3-7")]
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_an_inconsistent_legacy_vault_When_unlocking_and_saving_Then_the_flag_is_set_and_the_save_stays_in_the_original_legacy_format(bool sha1)
        {
            var hash = sha1 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256;
            var path = Path.Combine(_dir, "v.vault");
            File.WriteAllText(path, Convert.ToBase64String(LegacyCbc(Plain(WrongAddress), hash)));

            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.True(service.VaultIntegrityCheckFailed);
            Assert.False(Aes256GcmEncryptionStrategy.IsCurrentFormat(ReadBlob(path)));

            await service.SaveAsync();
            var saved = ReadBlob(path);
            Assert.False(Aes256GcmEncryptionStrategy.IsCurrentFormat(saved));
            Assert.Equal(0, saved.Length % 16);

            using var aes = Aes.Create();
            aes.Key = new Rfc2898DeriveBytes(Password, Encoding.UTF8.GetBytes("NethereumWallet16"), 10000, hash).GetBytes(32);
            aes.IV = saved.Take(16).ToArray();
            using var decryptor = aes.CreateDecryptor();
            var plain = Encoding.UTF8.GetString(decryptor.TransformFinalBlock(saved, 16, saved.Length - 16));
            Assert.Contains(WrongAddress, plain, StringComparison.OrdinalIgnoreCase);

            var reopened = new FileWalletVaultService(path);
            Assert.True(await reopened.UnlockAsync(Password));
            Assert.True(reopened.VaultIntegrityCheckFailed);
            Assert.Single(await reopened.GetAccountsAsync());
        }

        [Trait("Rule", "S9-R3-7")]
        [Fact]
        public async Task Given_a_flagged_service_When_a_later_unlock_finds_a_consistent_vault_Then_the_flag_clears_and_the_vault_migrates()
        {
            var path = Path.Combine(_dir, "v.vault");
            File.WriteAllText(path, Convert.ToBase64String(LegacyCbc(Plain(WrongAddress), HashAlgorithmName.SHA256)));
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.True(service.VaultIntegrityCheckFailed);

            File.WriteAllText(path, Convert.ToBase64String(LegacyCbc(Plain(RealAddress), HashAlgorithmName.SHA256)));
            Assert.True(await service.UnlockAsync(Password));
            Assert.False(service.VaultIntegrityCheckFailed);
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(ReadBlob(path)));
        }

        [Trait("Rule", "S9-R3-7")]
        [Fact]
        public async Task Given_a_consistent_legacy_vault_When_unlocking_Then_the_flag_is_not_set()
        {
            var path = Path.Combine(_dir, "v.vault");
            File.WriteAllText(path, Convert.ToBase64String(LegacyCbc(Plain(RealAddress), HashAlgorithmName.SHA256)));
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.False(service.VaultIntegrityCheckFailed);
        }
    }
}
