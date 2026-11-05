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
    public class S9VaultFormatTests
    {
        private const string Password = "vault password";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private const string Address = "0x12890d2cce102216644c59dae5baed380d84830c";
        private const int HeaderLength = 37;

        private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("{\"mnemonics\":[],\"accounts\":[]}");

        private static WalletVault BuildVault(IEncryptionStrategy strategy)
        {
            var vault = new WalletVault(strategy);
            vault.Accounts.Add(new PrivateKeyWalletAccount(Address, "cold", PrivateKeyHex));
            return vault;
        }

        private static byte[] LegacyAes(byte[] plaintext, string password, HashAlgorithmName hash)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateIV();
            aes.Key = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes("NethereumWallet16"), 10000, hash).GetBytes(32);
            using var encryptor = aes.CreateEncryptor();
            return aes.IV.Concat(encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length)).ToArray();
        }

        private static byte[] VaultPlaintext()
        {
            var keyStore = new KeyStoreEncryptionStrategy();
            var blob = Convert.FromBase64String(BuildVault(keyStore).Encrypt(Password));
            return keyStore.Decrypt(blob, Password);
        }

        [Trait("Rule", "S9-30")]
        [Fact]
        public void Given_a_plaintext_When_encrypting_with_the_gcm_strategy_Then_the_header_names_the_format_kdf_and_600000_iterations()
        {
            var blob = new Aes256GcmEncryptionStrategy().Encrypt(Plaintext, Password);
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(blob));
            Assert.Equal(new byte[] { 0x4E, 0x57, 0x56, 1, 1 }, blob.Take(5).ToArray());
            Assert.Equal(600000, (blob[5] << 24) | (blob[6] << 16) | (blob[7] << 8) | blob[8]);
            Assert.Equal(HeaderLength + Plaintext.Length + 16, blob.Length);
        }

        [Trait("Rule", "S9-30")]
        [Fact]
        public void Given_the_same_password_and_data_When_encrypting_twice_Then_salt_and_nonce_differ()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var a = strategy.Encrypt(Plaintext, Password);
            var b = strategy.Encrypt(Plaintext, Password);
            Assert.NotEqual(a.Skip(9).Take(16).ToArray(), b.Skip(9).Take(16).ToArray());
            Assert.NotEqual(a.Skip(25).Take(12).ToArray(), b.Skip(25).Take(12).ToArray());
        }

        [Trait("Rule", "S9-30")]
        [Fact]
        public void Given_a_blob_written_by_the_native_backend_When_decrypting_with_the_bouncycastle_backend_Then_both_directions_interoperate()
        {
            var native = new Aes256GcmEncryptionStrategy(false);
            var managed = new Aes256GcmEncryptionStrategy(true);
            Assert.Equal(Plaintext, managed.Decrypt(native.Encrypt(Plaintext, Password), Password));
            Assert.Equal(Plaintext, native.Decrypt(managed.Encrypt(Plaintext, Password), Password));
        }

        [Trait("Rule", "S9-30")]
        [Fact]
        public void Given_any_header_byte_flipped_When_decrypting_Then_the_blob_is_rejected()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var blob = strategy.Encrypt(Plaintext, Password);
            for (var i = 3; i < HeaderLength; i++)
            {
                var tampered = (byte[])blob.Clone();
                tampered[i] ^= 0x01;
                if (i == 5) tampered[i] = 0xFF;
                Assert.ThrowsAny<Exception>(() => strategy.Decrypt(tampered, Password));
            }
        }

        [Trait("Rule", "S9-30")]
        [Fact]
        public void Given_a_flipped_ciphertext_or_tag_bit_or_wrong_password_When_decrypting_Then_the_blob_is_rejected()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var blob = strategy.Encrypt(Plaintext, Password);
            var body = (byte[])blob.Clone();
            body[HeaderLength + 2] ^= 0x01;
            var tag = (byte[])blob.Clone();
            tag[tag.Length - 1] ^= 0x01;
            Assert.ThrowsAny<Exception>(() => strategy.Decrypt(body, Password));
            Assert.ThrowsAny<Exception>(() => strategy.Decrypt(tag, Password));
            Assert.ThrowsAny<Exception>(() => strategy.Decrypt(blob, "wrong"));
            Assert.ThrowsAny<Exception>(() => new Aes256GcmEncryptionStrategy(true).Decrypt(body, Password));
        }

        [Trait("Rule", "S9-32")]
        [Fact]
        public void Given_a_header_declaring_more_than_ten_million_iterations_When_decrypting_Then_it_is_rejected_before_deriving()
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var blob = strategy.Encrypt(Plaintext, Password);
            var iterations = 10000001;
            blob[5] = (byte)(iterations >> 24);
            blob[6] = (byte)(iterations >> 16);
            blob[7] = (byte)(iterations >> 8);
            blob[8] = (byte)iterations;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var ex = Assert.ThrowsAny<Exception>(() => strategy.Decrypt(blob, Password));
            Assert.Contains("out of range", ex.Message);
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024);
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public void Given_legacy_default_aes_and_bouncycastle_aes_blobs_When_decrypting_with_every_strategy_Then_each_reads_its_legacy_format()
        {
#pragma warning disable CS0618
            var sha256Blob = LegacyAes(Plaintext, Password, HashAlgorithmName.SHA256);
            var sha1Blob = LegacyAes(Plaintext, Password, HashAlgorithmName.SHA1);
            Assert.Equal(Plaintext, new DefaultAes256EncryptionStrategy().Decrypt(sha256Blob, Password));
            Assert.Equal(Plaintext, new BouncyCastleAes256EncryptionStrategy().Decrypt(sha1Blob, Password));
            Assert.Equal(Plaintext, new Aes256GcmEncryptionStrategy().Decrypt(sha256Blob, Password));
            Assert.Equal(Plaintext, new Aes256GcmEncryptionStrategy().Decrypt(sha1Blob, Password));
#pragma warning restore CS0618
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public void Given_a_legacy_keystore_blob_When_decrypting_with_the_gcm_strategy_Then_it_reads_it_under_the_keystore_caps()
        {
            var blob = new KeyStoreEncryptionStrategy().Encrypt(Plaintext, Password);
            Assert.Equal(Plaintext, new Aes256GcmEncryptionStrategy().Decrypt(blob, Password));
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public void Given_the_obsolete_aes_strategies_When_encrypting_Then_they_write_the_gcm_format()
        {
#pragma warning disable CS0618
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(new DefaultAes256EncryptionStrategy().Encrypt(Plaintext, Password)));
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(new BouncyCastleAes256EncryptionStrategy().Encrypt(Plaintext, Password)));
#pragma warning restore CS0618
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public async Task Given_a_legacy_file_vault_When_unlocking_Then_it_is_rewritten_in_the_gcm_format_and_still_unlocks()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".vault");
            try
            {
                var legacy = BuildVault(new KeyStoreEncryptionStrategy()).Encrypt(Password);
                File.WriteAllText(path, legacy);

                var service = new FileWalletVaultService(path);
                Assert.True(await service.UnlockAsync(Password));

                var migrated = File.ReadAllText(path);
                Assert.NotEqual(legacy, migrated);
                Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(Convert.FromBase64String(migrated)));
                Assert.False(File.Exists(path + ".tmp"));
                Assert.Single((await service.GetAccountsAsync()));

                var reopened = new FileWalletVaultService(path);
                Assert.True(await reopened.UnlockAsync(Password));
                Assert.Equal(migrated, File.ReadAllText(path));
                Assert.Equal(Address, (await reopened.GetAccountsAsync()).Single().Address);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public async Task Given_a_leftover_temporary_file_from_a_crashed_save_When_unlocking_a_legacy_vault_Then_the_old_blob_is_still_readable_and_migration_succeeds()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".vault");
            try
            {
                var legacy = BuildVault(new KeyStoreEncryptionStrategy()).Encrypt(Password);
                File.WriteAllText(path, legacy);
                File.WriteAllText(path + ".tmp", "truncated");

                var service = new FileWalletVaultService(path);
                Assert.True(await service.UnlockAsync(Password));
                Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(Convert.FromBase64String(File.ReadAllText(path))));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public async Task Given_a_custom_encryption_strategy_When_unlocking_Then_the_vault_is_not_migrated()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".vault");
            try
            {
                var custom = new ReversingStrategy();
                var blob = BuildVault(custom).Encrypt(Password);
                File.WriteAllText(path, blob);
                var service = new FileWalletVaultService(path, custom);
                Assert.True(await service.UnlockAsync(Password));
                Assert.Equal(blob, File.ReadAllText(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public async Task Given_a_new_vault_When_created_with_the_default_service_Then_it_is_stored_in_the_gcm_format()
        {
            var service = new SpyVaultService();
            await service.CreateNewAsync(Password);
            var blob = Convert.FromBase64String(service.Stored);
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(blob));
            Assert.True(await service.UnlockAsync(Password));
        }

        [Trait("Rule", "S9-35")]
        [Fact]
        public async Task Given_a_wrong_password_When_unlocking_Then_nothing_is_written_to_the_console()
        {
            var original = Console.Out;
            var captured = new StringWriter();
            Console.SetOut(captured);
            try
            {
                var service = new InMemoryWalletVaultService();
                await service.CreateNewAsync(Password);
                Assert.False(await service.UnlockAsync("not the password"));
            }
            finally
            {
                Console.SetOut(original);
            }
            Assert.DoesNotContain("Vault unlock failed", captured.ToString());
            Assert.DoesNotContain("not the password", captured.ToString());
        }

        [Trait("Rule", "S9-36")]
        [Fact]
        public void Given_a_private_key_wallet_account_When_reading_its_settings_Then_the_property_remains_and_the_text_omits_the_key()
        {
            var account = new PrivateKeyWalletAccount(Address, "cold", PrivateKeyHex);
            var settings = Assert.IsType<PrivateKeyAccountSettings>(account.Settings);
            Assert.Equal(PrivateKeyHex, settings.PrivateKey);
            Assert.DoesNotContain(PrivateKeyHex, settings.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private sealed class SpyVaultService : WalletVaultServiceBase
        {
            public string Stored { get; private set; }
            public override Task<bool> VaultExistsAsync() => Task.FromResult(Stored != null);
            protected override Task ResetStorageAsync() { Stored = null; return Task.CompletedTask; }
            protected override Task<string> GetEncryptedAsync() => Task.FromResult(Stored);
            protected override Task SaveEncryptedAsync(string encrypted) { Stored = encrypted; return Task.CompletedTask; }
        }

        private sealed class ReversingStrategy : IEncryptionStrategy
        {
            public byte[] Encrypt(byte[] data, string password) => data.Reverse().ToArray();
            public byte[] Decrypt(byte[] data, string password) => data.Reverse().ToArray();
        }
    }
}
