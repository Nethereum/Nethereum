using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nethereum.Accounts.Bip32;
using Nethereum.Wallet.WalletAccounts;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9.Round5
{
    public class S9Round5Tests : IDisposable
    {
        private const string Password = "vault password";
        private const string PrivateKeyHex = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "s9r5-" + Guid.NewGuid().ToString("N"));

        public S9Round5Tests()
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

        private static byte[] OracleSeed(string text)
        {
            using var derive = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetBytes("mnemonic"), 2048, HashAlgorithmName.SHA512);
            return derive.GetBytes(64);
        }

        private static string OracleCanonical(string raw)
        {
            var nfkd = raw.Normalize(NormalizationForm.FormKD);
            var words = Regex.Split(nfkd, "[\\u0009-\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]+")
                .Where(w => w.Length > 0);
            return string.Join(" ", words).ToLowerInvariant();
        }

        private static byte[] LegacyCbc(byte[] plain, byte[] ivPrefix = null)
        {
            var iv = RandomNumberGenerator.GetBytes(16);
            if (ivPrefix != null) Array.Copy(ivPrefix, iv, ivPrefix.Length);
            var key = new Rfc2898DeriveBytes(Password, Encoding.UTF8.GetBytes("NethereumWallet16"), 10000, HashAlgorithmName.SHA256).GetBytes(32);
            var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
            var output = new byte[cipher.GetOutputSize(plain.Length)];
            var length = cipher.ProcessBytes(plain, 0, plain.Length, output, 0);
            length += cipher.DoFinal(output, length);
            return iv.Concat(output.Take(length)).ToArray();
        }

        private static byte[] VaultJson(string phrase = null, string mismatchedAddress = null)
        {
            var vault = new WalletVault(new IdentityStrategy());
            if (phrase != null)
            {
                vault.AddMnemonic(new MnemonicInfo("m", phrase, "") { Id = "m1" });
                var hd = new MinimalHDWallet(phrase, "", false);
                vault.Accounts.Add(new MnemonicWalletAccount(hd.GetEthereumAddress(0), "hd", 0, "m1", hd));
            }
            vault.Accounts.Add(new PrivateKeyWalletAccount(mismatchedAddress ?? new Nethereum.Signer.EthECKey(PrivateKeyHex).GetPublicAddress(), "cold", PrivateKeyHex));
            return Convert.FromBase64String(vault.Encrypt(Password));
        }

        private async Task<string> WriteBlob(byte[] blob)
        {
            var path = NewPath();
            await File.WriteAllTextAsync(path, Convert.ToBase64String(blob));
            return path;
        }

        private static byte[] FileBlob(string path) => Convert.FromBase64String(File.ReadAllText(path));

        private static readonly string[] Separators =
        {
            " ", " ", " ", "  ", "\t", "\n", "\r\n", ((char)0x00A0).ToString(), ((char)0x3000).ToString(), ((char)0x2003).ToString(), ((char)0x202F).ToString(), ((char)0x2028).ToString(), ((char)0x0085).ToString(), ((char)0x1680).ToString(), ((char)0x200B).ToString(), ((char)0x00AD).ToString(), " " + ((char)0x0301).ToString()
        };

        private static IEnumerable<string> Corpus(int count)
        {
            var rng = new Random(2026);
            var accented = new[] { "caf"+((char)0x00E9).ToString(), "cafe"+((char)0x0301).ToString(), ((char)0x00C9).ToString()+"cole", ((char)0x0045).ToString()+((char)0x0301).ToString()+"cole", "x"+((char)0xFF21).ToString(), "x"+((char)0xFB01).ToString()+"nal", "x"+((char)0x01C5).ToString(), "x"+((char)0x1E9E).ToString(), ((char)0x0130).ToString()+"stanbul", "x"+((char)0x03A3).ToString()+((char)0x03C2).ToString(), "x"+((char)0xD55C).ToString()+((char)0xC5B4).ToString(), "x"+((char)0x3042).ToString()+((char)0x3093).ToString() };
            for (var i = 0; i < count; i++)
            {
                var words = Bip39.EntropyToMnemonic(RandomNumberGenerator.GetBytes(i % 2 == 0 ? 16 : 32)).Split(' ').ToList();
                if (i % 7 == 0) words[rng.Next(words.Count)] = accented[rng.Next(accented.Length)];
                if (i % 5 == 1) words[rng.Next(words.Count)] = words[rng.Next(words.Count)].ToUpperInvariant();
                if (i % 11 == 2) words[0] = char.ToUpperInvariant(words[0][0]) + words[0].Substring(1);
                if (i % 13 == 3) words[rng.Next(words.Count)] = "TRUE";
                var sb = new StringBuilder();
                var shape = i % 4;
                if (shape == 3) sb.Append(Separators[rng.Next(Separators.Length)]);
                for (var w = 0; w < words.Count; w++)
                {
                    if (w > 0) sb.Append(shape == 0 ? " " : Separators[rng.Next(Separators.Length)]);
                    sb.Append(words[w]);
                }
                if (shape == 3 || i % 17 == 5) sb.Append(Separators[rng.Next(Separators.Length)]);
                yield return sb.ToString();
            }
        }

        [Trait("Rule", "S9-R5-1")]
        [Fact]
        public void Given_a_corpus_of_6000_stored_phrase_shapes_When_comparing_the_flag_with_an_independent_pbkdf2_oracle_Then_it_is_set_exactly_when_the_7_0_seed_differs_from_the_standard_seed()
        {
            var falseNegatives = new List<string>();
            var falsePositives = new List<string>();
            var flagged = 0;
            foreach (var raw in Corpus(6000))
            {
                var legacySeed = OracleSeed(raw.Normalize(NormalizationForm.FormKD));
                var standardSeed = OracleSeed(OracleCanonical(raw));
                var differs = !legacySeed.SequenceEqual(standardSeed);
                var flag = new MnemonicInfo("l", raw).RequiresStandardSeedMigration;
                if (flag) flagged++;
                if (differs && !flag && falseNegatives.Count < 10) falseNegatives.Add(Escape(raw));
                if (!differs && flag && falsePositives.Count < 10) falsePositives.Add(Escape(raw));
            }
            Assert.True(flagged > 1000, "corpus did not exercise the flag: " + flagged);
            Assert.True(falseNegatives.Count == 0 && falsePositives.Count == 0,
                "non-standard but unflagged: [" + string.Join(" | ", falseNegatives) + "] standard but flagged: [" + string.Join(" | ", falsePositives) + "]");
        }

        private static string Escape(string s) => string.Concat(s.Select(c => c < 0x20 || c > 0x7E ? "\\u" + ((int)c).ToString("X4") : c.ToString()));

        [Trait("Rule", "S9-R5-2")]
        [Fact]
        public void Given_standard_phrases_of_every_size_When_checking_the_flag_Then_none_is_flagged()
        {
            foreach (var size in new[] { 12, 15, 18, 21, 24 })
                for (var i = 0; i < 400; i++)
                {
                    var phrase = Bip39.GenerateMnemonic(size);
                    Assert.False(new MnemonicInfo("l", phrase).RequiresStandardSeedMigration, phrase);
                    Assert.False(Bip39.IsNonStandardMnemonicForm(phrase));
                }
        }

        [Trait("Rule", "S9-R5-3")]
        [Fact]
        public void Given_a_non_standard_phrase_longer_than_4096_characters_When_flagging_and_loading_Then_it_is_flagged_and_still_loads_as_in_7_0()
        {
            var standard = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var longPhrase = standard + new string(' ', 5000);
            var legacySeed = OracleSeed(longPhrase);
            var standardSeed = OracleSeed(standard);
            Assert.False(legacySeed.SequenceEqual(standardSeed));
            Assert.True(new MnemonicInfo("l", longPhrase).RequiresStandardSeedMigration, "non-standard 7.0 phrase hidden by the 4096 cap");
            Assert.Equal(legacySeed.Length, new MinimalHDWallet(longPhrase, "", false).Seed.Length);
        }

        [Trait("Rule", "S9-R5-3")]
        [Fact]
        public async Task Given_a_7_0_vault_whose_stored_phrase_is_padded_beyond_4096_characters_When_unlocking_Then_the_vault_still_unlocks()
        {
            var longPhrase = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about" + new string('\n', 4200);
            var path = await WriteBlob(LegacyCbc(VaultJson(longPhrase)));
            Assert.True(await new FileWalletVaultService(path).UnlockAsync(Password), "one over-long stored phrase locks the whole vault");
        }

        [Trait("Rule", "S9-R5-4")]
        [Fact]
        public void Given_null_blank_and_boundary_inputs_When_flagging_Then_nothing_throws()
        {
            Assert.False(Bip39.IsNonStandardMnemonicForm(null));
            Assert.False(Bip39.IsNonStandardMnemonicForm(""));
            Assert.False(Bip39.IsNonStandardMnemonicForm("   "));
            Assert.False(new MnemonicInfo().RequiresStandardSeedMigration);
            Assert.True(Bip39.IsNonStandardMnemonicForm("Abandon"));
            Assert.False(Bip39.IsNonStandardMnemonicForm("abandon"));
            Assert.False(Bip39.IsNonStandardMnemonicForm(new string('a', 4096)));
            Assert.True(Bip39.IsNonStandardMnemonicForm(new string('a', 4095) + " "));
        }

        [Trait("Rule", "S9-R5-5")]
        [Fact]
        public async Task Given_keystore_vault_blobs_with_leading_whitespace_edges_When_unlocking_Then_only_json_whitespace_is_skipped()
        {
            var plain = VaultJson();
            var keystore = new KeyStoreEncryptionStrategy().Encrypt(plain, Password);
            foreach (var prefix in new[] { " \t\r\n ", "\n\n\n" })
            {
                var path = await WriteBlob(Encoding.UTF8.GetBytes(prefix).Concat(keystore).ToArray());
                var service = new FileWalletVaultService(path);
                Assert.True(await service.UnlockAsync(Password), "prefix " + Escape(prefix));
                Assert.False(service.VaultIntegrityCheckFailed);
            }
            foreach (var prefix in new byte[][] { new byte[] { 0x0B }, new byte[] { 0x0C }, new byte[] { 0xC2, 0xA0 } })
            {
                var path = await WriteBlob(prefix.Concat(keystore).ToArray());
                Assert.False(await new FileWalletVaultService(path).UnlockAsync(Password));
            }
        }

        [Trait("Rule", "S9-R5-5")]
        [Fact]
        public async Task Given_legacy_cbc_vaults_whose_first_bytes_look_like_whitespace_then_a_brace_When_unlocking_Then_every_one_still_unlocks()
        {
            var plain = VaultJson();
            var strategy = new Aes256GcmEncryptionStrategy();
            var failures = 0;
            foreach (var first in new byte[] { 0x20, 0x09, 0x0D, 0x0A })
                for (var i = 0; i < 150; i++)
                {
                    var blob = LegacyCbc(plain, new[] { first, (byte)'{' });
                    try { if (!strategy.Decrypt(blob, Password).SequenceEqual(plain)) failures++; } catch (Exception) { failures++; }
                }
            Assert.Equal(0, failures);
            await Task.CompletedTask;
        }

        [Trait("Rule", "S9-R5-5")]
        [Fact]
        public async Task Given_blobs_of_megabytes_of_whitespace_or_a_json_array_When_unlocking_Then_they_fail_quickly_without_exceptions()
        {
            foreach (var blob in new[]
            {
                Encoding.UTF8.GetBytes(new string(' ', 5_000_000)),
                Encoding.UTF8.GetBytes(new string(' ', 5_000_000) + "{"),
                Encoding.UTF8.GetBytes("  [ {\"crypto\":{}} ]"),
                new byte[] { 0x20 }, new byte[] { 0x7B }, new byte[0]
            })
            {
                var path = await WriteBlob(blob);
                bool unlocked;
                try { unlocked = await new FileWalletVaultService(path).UnlockAsync(Password); } catch (Exception) { unlocked = true; }
                Assert.False(unlocked);
            }
        }

        [Trait("Rule", "S9-R5-6")]
        [Fact]
        public async Task Given_the_flag_is_set_When_creating_a_new_vault_in_either_way_Then_the_flag_is_cleared_and_saves_are_gcm()
        {
            foreach (var withAccount in new[] { false, true })
            {
                var path = await WriteBlob(LegacyCbc(VaultJson(null, "0x2222222222222222222222222222222222222222")));
                var service = new FileWalletVaultService(path);
                await service.UnlockAsync(Password);
                Assert.True(service.VaultIntegrityCheckFailed);
                if (withAccount)
                    await service.CreateNewVaultWithAccountAsync("pw2", new ViewOnlyWalletAccount("0x1111111111111111111111111111111111111111", "w"));
                else
                    await service.CreateNewAsync("pw2");
                Assert.False(service.VaultIntegrityCheckFailed);
                await service.SaveAsync();
                Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(FileBlob(path)));
                Assert.True(await new FileWalletVaultService(path).UnlockAsync("pw2"));
            }
        }

        [Trait("Rule", "S9-R5-6")]
        [Fact]
        public async Task Given_the_flag_is_set_When_a_failed_create_throws_Then_the_service_is_not_left_claiming_a_consistent_vault_that_was_never_written()
        {
            var path = await WriteBlob(LegacyCbc(VaultJson(null, "0x2222222222222222222222222222222222222222")));
            var service = new FileWalletVaultService(path);
            await service.UnlockAsync(Password);
            Directory.CreateDirectory(path + ".tmp");
            await Assert.ThrowsAnyAsync<Exception>(() => service.CreateNewAsync("pw2"));
            Assert.Equal(await new FileWalletVaultService(path).UnlockAsync(Password), true);
            Directory.Delete(path + ".tmp");
        }
    }
}
