using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.KeyStore;
using Nethereum.KeyStore.Model;
using Nethereum.Wallet.WalletAccounts;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9LegacyVariantDetectionTests : IDisposable
    {
        private const string Password = "vault password";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private const string RealAddress = "0x008aeeda4d805471df9b2a5b0f38a0c3bcba786b";
        private const string WrongAddress = "0x2222222222222222222222222222222222222222";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "s9var-" + Guid.NewGuid().ToString("N"));

        public S9LegacyVariantDetectionTests()
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

        private static byte[] LegacyCbc(byte[] plain, HashAlgorithmName hash, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.IV = iv;
            aes.Key = new Rfc2898DeriveBytes(Password, Encoding.UTF8.GetBytes("NethereumWallet16"), 10000, hash).GetBytes(32);
            using var encryptor = aes.CreateEncryptor();
            return iv.Concat(encryptor.TransformFinalBlock(plain, 0, plain.Length)).ToArray();
        }

        private static byte[] FixedIv(byte first)
        {
            var iv = Enumerable.Range(1, 16).Select(i => (byte)(i * 7)).ToArray();
            iv[0] = first;
            return iv;
        }

        private static byte[] LightKeyStore(byte[] plain)
        {
            var store = new KeyStoreScryptService().EncryptAndGenerateCryptoStore(Password, plain,
                new ScryptParams { Dklen = 32, N = 1024, R = 8, P = 1 });
            var json = Nethereum.KeyStore.JsonDeserialisation.JsonCryptoStoreSerialiser.Serialise(store);
            return Encoding.UTF8.GetBytes(json);
        }

        [Trait("Rule", "S9-R3-LOCKOUT")]
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_a_legacy_cbc_vault_whose_iv_starts_with_an_opening_brace_When_unlocking_Then_it_unlocks_and_migrates(bool sha1)
        {
            var blob = LegacyCbc(Plain(RealAddress), sha1 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256, FixedIv((byte)'{'));
            var path = Path.Combine(_dir, "brace.vault");
            File.WriteAllText(path, Convert.ToBase64String(blob));

            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.False(service.VaultIntegrityCheckFailed);
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(Convert.FromBase64String(File.ReadAllText(path))));
        }

        [Trait("Rule", "S9-R3-LOCKOUT")]
        [Fact]
        public async Task Given_an_inconsistent_legacy_cbc_vault_whose_iv_starts_with_an_opening_brace_When_unlocking_Then_the_consistency_check_still_applies()
        {
            var blob = LegacyCbc(Plain(WrongAddress), HashAlgorithmName.SHA256, FixedIv((byte)'{'));
            var path = Path.Combine(_dir, "brace-bad.vault");
            File.WriteAllText(path, Convert.ToBase64String(blob));

            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.True(service.VaultIntegrityCheckFailed);
            Assert.False(Aes256GcmEncryptionStrategy.IsCurrentFormat(Convert.FromBase64String(File.ReadAllText(path))));
        }

        [Trait("Rule", "S9-R3-LOCKOUT")]
        [Fact]
        public void Given_thousands_of_random_legacy_vaults_of_every_format_When_decrypting_Then_every_one_is_read()
        {
            var plain = Plain(RealAddress);
            var strategy = new Aes256GcmEncryptionStrategy();
            var failures = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 2000; i++)
            {
                var iv = new byte[16];
                RandomNumberGenerator.Fill(iv);
                if (i % 10 == 0) iv[0] = (byte)'{';
                foreach (var hash in new[] { HashAlgorithmName.SHA256, HashAlgorithmName.SHA1 })
                {
                    var blob = LegacyCbc(plain, hash, iv);
                    try
                    {
                        if (!strategy.Decrypt(blob, Password).SequenceEqual(plain)) failures.Add(hash.Name + " " + i + " differs");
                    }
                    catch (Exception ex) { failures.Add(hash.Name + " " + i + " " + ex.GetType().Name); }
                }
                try
                {
                    if (!strategy.Decrypt(LightKeyStore(plain), Password).SequenceEqual(plain)) failures.Add("keystore " + i + " differs");
                }
                catch (Exception ex) { failures.Add("keystore " + i + " " + ex.GetType().Name); }
            }
            Assert.True(failures.Count == 0, failures.Count + " failures, first: " + string.Join("; ", failures.Take(5)));
        }
    }
}
