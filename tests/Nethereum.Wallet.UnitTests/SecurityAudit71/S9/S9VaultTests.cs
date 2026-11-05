using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore;
using Nethereum.Wallet.WalletAccounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9VaultTests
    {
        private const string Password = "vault password";
        private const string Mnemonic =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";

        private static IEncryptionStrategy[] Strategies() => new IEncryptionStrategy[]
        {
            new DefaultAes256EncryptionStrategy(),
            new BouncyCastleAes256EncryptionStrategy()
        };

        [Trait("Rule", "S9-30")]
        [Fact]
        public void Given_an_aes_vault_blob_with_a_flipped_iv_bit_When_decrypting_Then_the_tampering_is_detected()
        {
            var offenders = new System.Collections.Generic.List<string>();
            var plaintext = Encoding.UTF8.GetBytes("{\"send\":\"1 ETH to 0xAAAA\",\"pad\":\"xxxxxxxxxxxxxxxxxxxx\"}");
            foreach (var strategy in Strategies())
            {
                var blob = strategy.Encrypt(plaintext, Password);
                blob[8] ^= 0x01;
                byte[] decrypted = null;
                try { decrypted = strategy.Decrypt(blob, Password); } catch (Exception) { }
                if (decrypted != null)
                    offenders.Add(strategy.GetType().Name + " accepted a modified IV and returned " + Encoding.UTF8.GetString(decrypted));
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-30")]
        [Fact]
        public void Given_an_aes_vault_blob_with_modified_ciphertext_blocks_When_decrypting_Then_the_tampering_is_detected()
        {
            var offenders = new System.Collections.Generic.List<string>();
            var plaintext = Encoding.UTF8.GetBytes(new string('a', 16) + new string('b', 16) + new string('c', 16));
            foreach (var strategy in Strategies())
            {
                var blob = strategy.Encrypt(plaintext, Password);
                blob[16 + 3] ^= 0x01;
                byte[] decrypted = null;
                try { decrypted = strategy.Decrypt(blob, Password); } catch (Exception) { }
                if (decrypted != null) offenders.Add(strategy.GetType().Name + " returned plaintext for a modified ciphertext block");
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public void Given_an_aes_vault_blob_When_deriving_the_key_with_the_published_constant_salt_Then_it_does_not_decrypt()
        {
            var offenders = new System.Collections.Generic.List<string>();
            var plaintext = Encoding.UTF8.GetBytes("secret vault contents 123456789");
            foreach (var strategy in Strategies())
            {
                var blob = strategy.Encrypt(plaintext, Password);
                using var aes = Aes.Create();
                aes.Key = new Rfc2898DeriveBytes(Password, Encoding.UTF8.GetBytes("NethereumWallet16"), 10000, strategy is BouncyCastleAes256EncryptionStrategy ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256).GetBytes(32);
                aes.IV = blob.Take(16).ToArray();
                byte[] decrypted = null;
                try
                {
                    using var decryptor = aes.CreateDecryptor();
                    decrypted = decryptor.TransformFinalBlock(blob, 16, blob.Length - 16);
                }
                catch (CryptographicException) { }
                if (decrypted != null) offenders.Add(strategy.GetType().Name + " key is a function of the password alone: 10000 iterations and a constant salt");
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-31")]
        [Fact]
        public void Given_the_same_password_and_data_When_encrypting_twice_Then_the_blob_carries_a_per_vault_salt_and_a_mac()
        {
            var offenders = new System.Collections.Generic.List<string>();
            var plaintext = Encoding.UTF8.GetBytes("secret vault contents 123456789");
            foreach (var strategy in Strategies())
            {
                var a = strategy.Encrypt(plaintext, Password);
                var b = strategy.Encrypt(plaintext, Password);
                Assert.NotEqual(a.Take(16).ToArray().ToHex(), b.Take(16).ToArray().ToHex());
                if (a.Length <= 16 + 32) offenders.Add(strategy.GetType().Name + " blob has no salt or MAC fields: length " + a.Length);
            }
            Assert.True(offenders.Count == 0, string.Join("; ", offenders));
        }

        [Trait("Rule", "S9-32")]
        [Fact]
        public void Given_a_vault_blob_with_hostile_scrypt_cost_When_decrypting_with_the_keystore_strategy_Then_it_is_rejected_before_allocating_a_gigabyte()
        {
            var strategy = new KeyStoreEncryptionStrategy();
            var blob = strategy.Encrypt(Encoding.UTF8.GetBytes("{}"), Password);
            var json = JObject.Parse(Encoding.UTF8.GetString(blob));
            json["crypto"]["kdfparams"]["n"] = 1 << 20;
            json["crypto"]["kdfparams"]["r"] = 8;
            var hostile = Encoding.UTF8.GetBytes(json.ToString());
            var before = GC.GetAllocatedBytesForCurrentThread();
            try { strategy.Decrypt(hostile, Password); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 64L * 1024 * 1024, "allocated " + allocated + " bytes");
        }

        [Trait("Rule", "S9-33")]
        [Fact]
        public void Given_a_keystore_strategy_vault_with_a_flipped_ciphertext_nibble_When_unlocking_Then_it_is_rejected()
        {
            var vault = BuildVault(new KeyStoreEncryptionStrategy());
            var encrypted = vault.Encrypt(Password);
            var json = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encrypted)));
            var cipherText = (string)json["crypto"]["ciphertext"];
            json["crypto"]["ciphertext"] = (cipherText[0] == '0' ? "1" : "0") + cipherText.Substring(1);
            var tampered = Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToString()));
            var target = new WalletVault(new KeyStoreEncryptionStrategy());
            Assert.ThrowsAny<Exception>(() => target.Decrypt(tampered, Password));
            Assert.Empty(target.Mnemonics);
        }

        [Trait("Rule", "S9-34")]
        [Fact]
        public void Given_any_vault_strategy_When_encrypting_Then_the_blob_contains_no_mnemonic_or_private_key_in_the_clear()
        {
            foreach (var strategy in new IEncryptionStrategy[] { new KeyStoreEncryptionStrategy(), new DefaultAes256EncryptionStrategy(), new BouncyCastleAes256EncryptionStrategy() })
            {
                var blob = BuildVault(strategy).Encrypt(Password);
                var raw = Convert.FromBase64String(blob);
                var text = Encoding.UTF8.GetString(raw);
                Assert.DoesNotContain("abandon", text);
                Assert.DoesNotContain(PrivateKeyHex, text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("mnemonic", text);
                Assert.DoesNotContain("passphrase", text);
            }
        }

        [Trait("Rule", "S9-35")]
        [Fact]
        public async Task Given_a_wrong_password_When_unlocking_a_vault_Then_no_log_file_is_written_to_the_working_directory()
        {
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "VaultUnlock.log");
            if (File.Exists(logPath)) File.Delete(logPath);
            try
            {
                var service = new InMemoryWalletVaultService();
                await service.CreateNewAsync(Password);
                var unlocked = await service.UnlockAsync("not the password");
                Assert.False(unlocked);
                Assert.False(File.Exists(logPath), "VaultUnlock.log was written to " + logPath);
            }
            finally
            {
                if (File.Exists(logPath)) File.Delete(logPath);
            }
        }

        [Trait("Rule", "S9-36")]
        [Fact]
        public void Given_a_private_key_wallet_account_When_formatting_its_settings_Then_the_private_key_is_not_in_the_text()
        {
            var account = new PrivateKeyWalletAccount("0x12890d2cce102216644c59dae5baed380d84830c", "cold", PrivateKeyHex);
            Assert.DoesNotContain(PrivateKeyHex, account.Settings.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Trait("Rule", "S9-37")]
        [Fact]
        public void Given_a_vault_with_a_mnemonic_account_When_decrypting_a_tampered_json_payload_Then_failures_do_not_echo_the_mnemonic()
        {
            var strategy = new KeyStoreEncryptionStrategy();
            var vault = new WalletVault(strategy);
            vault.AddMnemonic(new MnemonicInfo("main", Mnemonic, "my passphrase") { Id = "m1" });
            var json = "{\"mnemonics\":[{\"label\":\"main\",\"mnemonic\":\"" + Mnemonic +
                       "\",\"passphrase\":\"my passphrase\",\"id\":\"m1\"}],\"accounts\":[{\"type\":\"mnemonic\",\"address\":\"0x1\",\"label\":\"x\",\"index\":\"notanumber\",\"mnemonicId\":\"m1\"}]}";
            var blob = Convert.ToBase64String(strategy.Encrypt(Encoding.UTF8.GetBytes(json), Password));
            var ex = Assert.ThrowsAny<Exception>(() => new WalletVault(strategy).Decrypt(blob, Password));
            var text = ex.ToString();
            Assert.DoesNotContain("abandon", text);
            Assert.DoesNotContain("my passphrase", text);
            Assert.DoesNotContain(Password, text);
        }

        private static WalletVault BuildVault(IEncryptionStrategy strategy)
        {
            var vault = new WalletVault(strategy);
            vault.AddMnemonic(new MnemonicInfo("main", Mnemonic, "my passphrase") { Id = "m1" });
            vault.Accounts.Add(new PrivateKeyWalletAccount("0x12890d2cce102216644c59dae5baed380d84830c", "cold", PrivateKeyHex));
            return vault;
        }
    }
}
