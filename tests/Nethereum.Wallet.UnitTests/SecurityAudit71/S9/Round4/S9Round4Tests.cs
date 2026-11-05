using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Wallet.WalletAccounts;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9.Round4
{
    public class S9Round4Tests : IDisposable
    {
        private const string Password = "vault password";
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "s9r4-" + Guid.NewGuid().ToString("N"));

        public S9Round4Tests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        private string NewPath() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".vault");

        private class IdentityStrategy : IEncryptionStrategy
        {
            public byte[] Encrypt(byte[] data, string password) => data;
            public byte[] Decrypt(byte[] data, string password) => data;
        }

        private static string KeyAddress(string key) => new Nethereum.Signer.EthECKey(key).GetPublicAddress();

        private static byte[] VaultJson(string mismatchedAddress = null, string phrase = null)
        {
            var vault = new WalletVault(new IdentityStrategy());
            if (phrase != null)
            {
                vault.AddMnemonic(new MnemonicInfo("m", phrase, "") { Id = "m1" });
                var hd = new MinimalHDWallet(phrase, "", false);
                vault.Accounts.Add(new MnemonicWalletAccount(hd.GetEthereumAddress(0), "hd", 0, "m1", hd));
            }
            vault.Accounts.Add(new PrivateKeyWalletAccount(mismatchedAddress ?? KeyAddress(PrivateKeyHex), "cold", PrivateKeyHex));
            vault.AddOrUpdateHardwareDevice("d1", "ledger", "dev");
            return Convert.FromBase64String(vault.Encrypt(Password));
        }

        private static byte[] LegacyCbc(byte[] plain, bool sha1, byte first = 0, bool setFirst = false)
        {
            var iv = RandomNumberGenerator.GetBytes(16);
            if (setFirst) iv[0] = first;
            var salt = Encoding.UTF8.GetBytes("NethereumWallet16");
            byte[] key;
            if (sha1)
            {
                var generator = new Pkcs5S2ParametersGenerator();
                generator.Init(Encoding.UTF8.GetBytes(Password), salt, 10000);
                key = ((KeyParameter)generator.GenerateDerivedParameters("AES", 256)).GetKey();
            }
            else key = new Rfc2898DeriveBytes(Password, salt, 10000, HashAlgorithmName.SHA256).GetBytes(32);
            var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
            var output = new byte[cipher.GetOutputSize(plain.Length)];
            var length = cipher.ProcessBytes(plain, 0, plain.Length, output, 0);
            length += cipher.DoFinal(output, length);
            return iv.Concat(output.Take(length)).ToArray();
        }

        private static bool DecryptsAs(byte[] blob, bool sha1)
        {
            try
            {
                var salt = Encoding.UTF8.GetBytes("NethereumWallet16");
                byte[] key;
                if (sha1)
                {
                    var generator = new Pkcs5S2ParametersGenerator();
                    generator.Init(Encoding.UTF8.GetBytes(Password), salt, 10000);
                    key = ((KeyParameter)generator.GenerateDerivedParameters("AES", 256)).GetKey();
                }
                else key = new Rfc2898DeriveBytes(Password, salt, 10000, HashAlgorithmName.SHA256).GetBytes(32);
                var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
                cipher.Init(false, new ParametersWithIV(new KeyParameter(key), blob.Take(16).ToArray()));
                var body = blob.Skip(16).ToArray();
                var output = new byte[cipher.GetOutputSize(body.Length)];
                var length = cipher.ProcessBytes(body, 0, body.Length, output, 0);
                length += cipher.DoFinal(output, length);
                System.Text.Json.JsonDocument.Parse(output.Take(length).ToArray());
                return true;
            }
            catch (Exception) { return false; }
        }

        private async Task<string> WriteBlob(byte[] blob)
        {
            var path = NewPath();
            await File.WriteAllTextAsync(path, Convert.ToBase64String(blob));
            return path;
        }

        private static byte[] FileBlob(string path) => Convert.FromBase64String(File.ReadAllText(path));

        [Trait("Rule", "S9-R4-1")]
        [Fact]
        public void Given_hundreds_of_legacy_cbc_vaults_whose_first_byte_is_0x7b_in_each_variant_When_decrypting_Then_every_one_unlocks()
        {
            var plain = VaultJson();
            var strategy = new Aes256GcmEncryptionStrategy();
            var failures = 0;
            for (var i = 0; i < 300; i++)
                foreach (var sha1 in new[] { false, true })
                {
                    var blob = LegacyCbc(plain, sha1, 0x7B, true);
                    try { if (!strategy.Decrypt(blob, Password).SequenceEqual(plain)) failures++; } catch (Exception) { failures++; }
                }
            Assert.Equal(0, failures);
        }

        [Trait("Rule", "S9-R4-1")]
        [Fact]
        public void Given_many_random_legacy_vaults_of_both_variants_When_decrypting_Then_the_wrong_variant_is_never_accepted_first()
        {
            var plain = VaultJson();
            var strategy = new Aes256GcmEncryptionStrategy();
            var failures = 0;
            for (var i = 0; i < 1500; i++)
                foreach (var sha1 in new[] { false, true })
                {
                    var blob = LegacyCbc(plain, sha1);
                    try { if (!strategy.Decrypt(blob, Password).SequenceEqual(plain)) failures++; } catch (Exception) { failures++; }
                }
            Assert.Equal(0, failures);
        }

        [Trait("Rule", "S9-R4-2")]
        [Fact]
        public async Task Given_a_keystore_vault_with_leading_whitespace_or_a_bom_When_unlocking_Then_it_unlocks_like_the_keystore_strategy_does()
        {
            var plain = VaultJson();
            var keystore = new KeyStoreEncryptionStrategy().Encrypt(plain, Password);
            var failures = new List<string>();
            foreach (var prefix in new[] { " ", "\n", "\r\n", "\t", "\uFEFF" })
            {
                var blob = Encoding.UTF8.GetBytes(prefix).Concat(keystore).ToArray();
                bool keyStoreStrategyReads;
                try { keyStoreStrategyReads = new KeyStoreEncryptionStrategy().Decrypt(blob, Password).SequenceEqual(plain); }
                catch (Exception) { keyStoreStrategyReads = false; }
                if (!keyStoreStrategyReads) continue;
                var path = await WriteBlob(blob);
                if (!await new FileWalletVaultService(path).UnlockAsync(Password)) failures.Add("prefix " + prefix.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\uFEFF", "BOM"));
            }
            Assert.True(failures.Count == 0, "readable by the keystore strategy but locked out by the default service: " + string.Join(", ", failures));
        }

        [Trait("Rule", "S9-R4-3")]
        [Fact]
        public async Task Given_a_legacy_vault_whose_plaintext_is_an_array_scalar_or_empty_object_When_unlocking_Then_unlock_returns_false_and_the_file_is_untouched()
        {
            var failures = new List<string>();
            foreach (var plain in new[] { "[]", "1", "\"x\"", "null", "true", "{}", "[{\"mnemonics\":[]}]", "{\"mnemonics\":1,\"accounts\":[]}", "{\"mnemonics\":[],\"accounts\":{}}" })
                foreach (var sha1 in new[] { false, true })
                {
                    var blob = LegacyCbc(Encoding.UTF8.GetBytes(plain), sha1);
                    var path = await WriteBlob(blob);
                    try
                    {
                        if (await new FileWalletVaultService(path).UnlockAsync(Password)) failures.Add(plain + " unlocked");
                    }
                    catch (Exception ex) { failures.Add(plain + " threw " + ex.GetType().Name); }
                    if (!FileBlob(path).SequenceEqual(blob)) failures.Add(plain + " file changed");
                }
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Trait("Rule", "S9-R4-4")]
        [Fact]
        public async Task Given_an_inconsistent_legacy_vault_of_either_variant_When_unlocking_and_saving_Then_the_flag_is_set_and_the_save_stays_in_the_same_legacy_variant()
        {
            foreach (var sha1 in new[] { false, true })
            {
                var plain = VaultJson("0x2222222222222222222222222222222222222222");
                var path = await WriteBlob(LegacyCbc(plain, sha1));
                var service = new FileWalletVaultService(path);
                Assert.True(await service.UnlockAsync(Password));
                Assert.True(service.VaultIntegrityCheckFailed);
                await service.SaveAsync();
                var saved = FileBlob(path);
                Assert.False(Aes256GcmEncryptionStrategy.IsCurrentFormat(saved));
                Assert.True(DecryptsAs(saved, sha1), "variant " + sha1);
                Assert.False(DecryptsAs(saved, !sha1));
                await service.SaveAsync("changed");
                Assert.False(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)));
            }
        }

        [Trait("Rule", "S9-R4-4")]
        [Fact]
        public async Task Given_the_mismatched_account_is_removed_and_saved_When_unlocking_again_Then_the_flag_clears_and_the_vault_migrates()
        {
            var path = await WriteBlob(LegacyCbc(VaultJson("0x2222222222222222222222222222222222222222"), false));
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            service.GetCurrentVault().Accounts.Clear();
            await service.SaveAsync();
            var again = new FileWalletVaultService(path);
            Assert.True(await again.UnlockAsync(Password));
            Assert.False(again.VaultIntegrityCheckFailed);
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)));
        }

        [Trait("Rule", "S9-R4-5")]
        [Fact]
        public async Task Given_the_flag_is_set_When_locking_resetting_or_unlocking_a_consistent_vault_Then_the_flag_clears()
        {
            var path = await WriteBlob(LegacyCbc(VaultJson("0x2222222222222222222222222222222222222222"), false));
            var service = new FileWalletVaultService(path);
            await service.UnlockAsync(Password);
            Assert.True(service.VaultIntegrityCheckFailed);
            Assert.False(await service.UnlockAsync("wrong"));
            Assert.True(service.VaultIntegrityCheckFailed);
            await service.LockAsync();
            Assert.False(service.VaultIntegrityCheckFailed);
            await service.UnlockAsync(Password);
            Assert.True(service.VaultIntegrityCheckFailed);
            await service.ResetAsync();
            Assert.False(service.VaultIntegrityCheckFailed);
            await File.WriteAllTextAsync(path, Convert.ToBase64String(LegacyCbc(VaultJson("0x2222222222222222222222222222222222222222"), false)));
            await service.UnlockAsync(Password);
            Assert.True(service.VaultIntegrityCheckFailed);
            await File.WriteAllTextAsync(path, Convert.ToBase64String(LegacyCbc(VaultJson(), false)));
            Assert.True(await service.UnlockAsync(Password));
            Assert.False(service.VaultIntegrityCheckFailed);
            await service.SaveAsync();
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)));
        }

        [Trait("Rule", "S9-R4-5")]
        [Fact]
        public async Task Given_the_flag_is_set_When_a_new_vault_is_created_on_the_same_service_Then_the_flag_does_not_describe_the_old_vault()
        {
            var path = await WriteBlob(LegacyCbc(VaultJson("0x2222222222222222222222222222222222222222"), false));
            var service = new FileWalletVaultService(path);
            await service.UnlockAsync(Password);
            Assert.True(service.VaultIntegrityCheckFailed);
            await service.CreateNewAsync("another password");
            Assert.False(service.VaultIntegrityCheckFailed);
        }

        [Trait("Rule", "S9-R4-5")]
        [Fact]
        public async Task Given_interleaved_unlock_save_and_lock_calls_on_one_service_When_they_finish_Then_the_file_still_decrypts_to_a_complete_vault_and_no_tmp_remains()
        {
            var path = await WriteBlob(LegacyCbc(VaultJson(), false));
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            var stop = DateTime.UtcNow.AddSeconds(4);
            var tasks = Enumerable.Range(0, 6).Select(i => Task.Run(async () =>
            {
                while (DateTime.UtcNow < stop)
                {
                    try
                    {
                        switch (i % 3)
                        {
                            case 0: await service.UnlockAsync(Password); break;
                            case 1: await service.SaveAsync(Password); break;
                            default: await service.LockAsync(); break;
                        }
                    }
                    catch (Exception) { }
                }
            })).ToArray();
            await Task.WhenAll(tasks);
            var fresh = new FileWalletVaultService(path);
            Assert.True(await fresh.UnlockAsync(Password));
            var vault = fresh.GetCurrentVault();
            Assert.Single(vault.Accounts);
            Assert.Single(vault.HardwareDevices);
            Assert.False(File.Exists(path + ".tmp"));
        }

        [Trait("Rule", "S9-R4-6")]
        [Fact]
        public async Task Given_the_flag_is_set_and_the_tmp_path_is_unwritable_When_saving_Then_the_save_fails_and_the_original_legacy_file_still_unlocks()
        {
            var blob = LegacyCbc(VaultJson("0x2222222222222222222222222222222222222222"), false);
            var path = await WriteBlob(blob);
            var service = new FileWalletVaultService(path);
            await service.UnlockAsync(Password);
            Directory.CreateDirectory(path + ".tmp");
            await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAsync());
            Assert.Equal(blob, FileBlob(path));
            Directory.Delete(path + ".tmp");
            Assert.True(await new FileWalletVaultService(path).UnlockAsync(Password));
        }

        [Trait("Rule", "S9-R4-7")]
        [Fact]
        public void Given_phrases_with_true_or_false_among_whitespace_and_unicode_spaces_When_detecting_Then_only_the_exact_uppercase_tokens_are_flagged()
        {
            var expectations = new Dictionary<string, bool>
            {
                ["abandon TRUE about"] = true, ["abandon\tFALSE\nabout"] = true, ["abandon\u00A0TRUE\u00A0about"] = true,
                ["abandon\u3000FALSE\u3000about"] = true, ["TRUE"] = true, ["FALSE abandon"] = true, ["abandon  TRUE  "] = true,
                ["abandon true about"] = false, ["abandon True about"] = false, ["abandon tRUE about"] = false,
                ["abandon TRUE, about"] = false, ["abandon TRUEX about"] = false, ["abandon XTRUE about"] = false,
                ["abandon TRU\u200BE about"] = false, ["abandon false about"] = false, [""] = false, [Abandon] = false
            };
            var wrong = expectations.Where(e => Bip39.IsLegacyUppercaseWordMnemonic(e.Key) != e.Value).Select(e => e.Key).ToList();
            Assert.True(wrong.Count == 0, string.Join(" | ", wrong));
        }

        [Trait("Rule", "S9-R4-8")]
        [Fact]
        public async Task Given_strict_imported_and_generated_canonical_phrases_When_stored_and_reloaded_Then_none_is_ever_flagged()
        {
            var vault = new WalletVault(new Aes256GcmEncryptionStrategy());
            var service = new CoreWalletAccountService(vault);
            for (var i = 0; i < 200; i++)
            {
                var phrase = Bip39.GenerateMnemonic(i % 2 == 0 ? 12 : 24);
                Assert.False(Bip39.IsLegacyUppercaseWordMnemonic(phrase));
                var messy = "  " + string.Join("  ", phrase.ToUpperInvariant().Split(' ')) + "\n";
                var account = await service.CreateMnemonicAccountAsync(messy, "", "l" + i);
                vault.Accounts.Add(account);
            }
            Assert.Empty(vault.AccountsWithLegacySeed);
            Assert.Empty(vault.MnemonicsWithLegacySeed);
            var reloaded = new WalletVault(new Aes256GcmEncryptionStrategy());
            reloaded.Decrypt(vault.Encrypt(Password), Password);
            Assert.Empty(reloaded.AccountsWithLegacySeed);
        }

        private static string SevenZeroPhrase()
        {
            for (var i = 0; i < 100000; i++)
            {
                var words = Bip39.EntropyToMnemonic(RandomNumberGenerator.GetBytes(16)).Split(' ');
                var index = Array.FindIndex(words, w => w == "true" || w == "false");
                if (index < 0) continue;
                words[index] = words[index].ToUpperInvariant();
                return string.Join(" ", words);
            }
            throw new InvalidOperationException();
        }

        [Trait("Rule", "S9-R4-9")]
        [Fact]
        public async Task Given_a_7_0_vault_with_an_uppercase_true_phrase_When_unlocked_migrated_and_unlocked_again_Then_the_flag_and_summaries_survive_every_step()
        {
            var path = await WriteBlob(LegacyCbc(VaultJson(null, SevenZeroPhrase()), false));
            var first = new FileWalletVaultService(path);
            Assert.True(await first.UnlockAsync(Password));
            Assert.True(first.HasAccountsWithLegacySeed);
            Assert.Single(first.AccountsWithLegacySeed);
            Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)));
            var second = new FileWalletVaultService(path);
            Assert.True(await second.UnlockAsync(Password));
            Assert.True(second.HasAccountsWithLegacySeed);
            Assert.Single(second.GetCurrentVault().MnemonicsWithLegacySeed);
            Assert.True(((MnemonicWalletAccount)second.AccountsWithLegacySeed.Single()).RequiresStandardSeedMigration);
            await second.SaveAsync();
            var third = new FileWalletVaultService(path);
            await third.UnlockAsync(Password);
            Assert.True(third.HasAccountsWithLegacySeed);
        }

        [Trait("Rule", "S9-R4-9")]
        [Fact]
        public async Task Given_an_inconsistent_vault_holding_a_legacy_seed_phrase_When_unlocked_Then_the_flag_still_reports_the_phrase()
        {
            var plain = VaultJson("0x2222222222222222222222222222222222222222", SevenZeroPhrase());
            var path = await WriteBlob(LegacyCbc(plain, true));
            var service = new FileWalletVaultService(path);
            Assert.True(await service.UnlockAsync(Password));
            Assert.True(service.VaultIntegrityCheckFailed);
            Assert.True(service.HasAccountsWithLegacySeed);
        }

        [Trait("Rule", "S9-R4-10")]
        [Fact]
        public async Task Given_stored_7_0_phrases_that_derive_a_non_standard_seed_for_other_reasons_than_true_or_false_When_loaded_Then_they_are_flagged_too()
        {
            var shapes = new Dictionary<string, string>
            {
                ["capitalised first word"] = "Abandon " + Abandon.Substring(8),
                ["all uppercase"] = Abandon.ToUpperInvariant(),
                ["double space"] = Abandon.Replace(" ", "  "),
                ["trailing newline"] = Abandon + "\n",
                ["tabs"] = Abandon.Replace(" ", "\t"),
                ["leading space"] = " " + Abandon
            };
            var unflagged = new List<string>();
            foreach (var shape in shapes)
            {
                var standard = new MinimalHDWallet(Abandon, "", false).Seed;
                var legacy = new MinimalHDWallet(shape.Value, "", false).Seed;
                Assert.False(standard.SequenceEqual(legacy), shape.Key + " fixture must derive a non-standard seed");
                var path = await WriteBlob(LegacyCbc(VaultJson(null, shape.Value), false));
                var service = new FileWalletVaultService(path);
                Assert.True(await service.UnlockAsync(Password));
                if (!service.HasAccountsWithLegacySeed) unflagged.Add(shape.Key);
            }
            Assert.True(unflagged.Count == 0, "non-standard wallets not flagged: " + string.Join(", ", unflagged));
        }
    }
}
