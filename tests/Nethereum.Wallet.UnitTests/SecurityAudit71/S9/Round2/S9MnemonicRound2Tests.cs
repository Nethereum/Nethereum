using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Wallet.WalletAccounts;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9.Round2
{
    public class S9MnemonicRound2Tests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        private static byte[] SevenZeroSeed(string mnemonic, string password)
        {
            var normalized = mnemonic.Normalize(NormalizationForm.FormKD);
            var salt = ("mnemonic" + password).Normalize(NormalizationForm.FormKD);
            using var derive = new Rfc2898DeriveBytes(normalized, Encoding.UTF8.GetBytes(salt), 2048, HashAlgorithmName.SHA512);
            return derive.GetBytes(64);
        }

        private static Dictionary<string, string> LegacyShapes() => new Dictionary<string, string>
        {
            ["uppercase"] = Abandon.ToUpperInvariant(),
            ["mixed case"] = "Abandon " + Abandon.Substring(8),
            ["double space"] = Abandon.Replace(" ", "  "),
            ["tabs"] = Abandon.Replace(" ", "\t"),
            ["newlines"] = Abandon.Replace(" ", "\n"),
            ["leading and trailing"] = "  " + Abandon + " \n",
            ["bad checksum"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon",
            ["25 words"] = Abandon + " abandon",
            ["13 words"] = Abandon + " zoo",
            ["not in list"] = "zzzzz " + Abandon.Substring(8),
            ["japanese ideographic space"] = "あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あいこくしん\u3000あおぞら",
            ["spanish accents"] = "ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco abierto",
            ["nfd form"] = "e\u0301cole " + Abandon.Substring(8),
            ["single word"] = "hello"
        };

        [Trait("Rule", "S9-R2-40")]
        [Fact]
        public void Given_every_mnemonic_shape_7_0_accepted_When_loading_leniently_Then_the_seed_equals_the_7_0_algorithm()
        {
            var failures = new List<string>();
            foreach (var shape in LegacyShapes())
                foreach (var pass in new[] { "", "TREZOR", "caf\u00e9" })
                {
                    try
                    {
                        var seed = new MinimalHDWallet(shape.Value, pass, false).Seed;
                        if (!seed.SequenceEqual(SevenZeroSeed(shape.Value, pass))) failures.Add(shape.Key + "/" + pass + " differs");
                    }
                    catch (Exception ex) { failures.Add(shape.Key + "/" + pass + " threw " + ex.GetType().Name); }
                }
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Trait("Rule", "S9-R2-41")]
        [Fact]
        public void Given_a_stored_vault_holding_every_legacy_mnemonic_shape_When_decrypting_Then_every_account_loads_with_the_7_0_address()
        {
            var failures = new List<string>();
            foreach (var shape in LegacyShapes())
            {
                var info = new MnemonicInfo("m", shape.Value, "pp") { Id = "m1" };
                var expected = new MinimalHDWallet(SevenZeroSeed(shape.Value, "pp")).GetEthereumAddress(0);
                var json = "{\"mnemonics\":[{\"label\":\"m\",\"mnemonic\":" + JsonSerializer.Serialize(shape.Value) +
                           ",\"passphrase\":\"pp\",\"id\":\"m1\"}],\"accounts\":[{\"type\":\"mnemonic\",\"address\":\"" + expected +
                           "\",\"label\":\"a\",\"index\":0,\"mnemonicId\":\"m1\"}],\"hardwareDevices\":[]}";
                var strategy = new Aes256GcmEncryptionStrategy();
                var blob = Convert.ToBase64String(strategy.Encrypt(Encoding.UTF8.GetBytes(json), "pw"));
                try
                {
                    var vault = new WalletVault(strategy);
                    vault.Decrypt(blob, "pw");
                    var account = vault.Accounts.Single().GetAccountAsync().GetAwaiter().GetResult();
                    if (!string.Equals(account.Address, expected, StringComparison.OrdinalIgnoreCase))
                        failures.Add(shape.Key + " address " + account.Address + " != " + expected);
                }
                catch (Exception ex) { failures.Add(shape.Key + " threw " + ex.GetType().Name + ": " + ex.Message); }
            }
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [Trait("Rule", "S9-R2-42")]
        [Fact]
        public void Given_shapes_that_are_not_valid_english_bip39_When_importing_strictly_Then_each_is_rejected()
        {
            var mustReject = new[]
            {
                "bad checksum", "25 words", "13 words", "not in list", "japanese ideographic space",
                "spanish accents", "single word"
            };
            var accepted = new List<string>();
            foreach (var key in mustReject)
            {
                try { new MinimalHDWallet(LegacyShapes()[key], ""); accepted.Add(key); }
                catch (ArgumentException) { }
            }
            Assert.True(accepted.Count == 0, "strict accepted: " + string.Join(", ", accepted));
        }

        [Trait("Rule", "S9-R2-43")]
        [Fact]
        public void Given_valid_mnemonics_of_every_size_When_importing_strictly_Then_each_is_accepted()
        {
            foreach (var size in new[] { 12, 15, 18, 21, 24 })
            {
                var mnemonic = Bip39.GenerateMnemonic(size);
                Assert.Equal(size, mnemonic.Split(' ').Length);
                Assert.NotNull(new MinimalHDWallet(mnemonic, "").GetEthereumAddress(0));
            }
        }

        [Trait("Rule", "S9-R2-44")]
        [Fact]
        public void Given_a_valid_mnemonic_with_extra_whitespace_When_importing_strictly_Then_the_seed_equals_the_canonical_bip39_seed()
        {
            var canonical = new MinimalHDWallet(Abandon, "").Seed.ToHex();
            var failures = new List<string>();
            foreach (var variant in new[] { Abandon + " ", " " + Abandon, Abandon.Replace(" ", "  "), Abandon + "\n", Abandon.Replace(" ", "\t") })
            {
                string seed;
                try { seed = new MinimalHDWallet(variant, "").Seed.ToHex(); }
                catch (ArgumentException) { continue; }
                if (seed != canonical) failures.Add("'" + variant.Replace("\n", "\\n").Replace("\t", "\\t") + "'");
            }
            Assert.True(failures.Count == 0, "strict import validated but derived a non-canonical wallet for: " + string.Join(", ", failures));
        }

        [Trait("Rule", "S9-R2-45")]
        [Fact]
        public void Given_an_uppercase_valid_mnemonic_When_importing_strictly_Then_it_is_accepted_like_the_ui_word_check_does()
        {
            Assert.NotNull(new MinimalHDWallet(Abandon.ToUpperInvariant(), "").GetEthereumAddress(0));
        }

        [Trait("Rule", "S9-R2-46")]
        [Trait("Supersededby", "S9-R5-A")]
        [Fact]
        public void Given_inputs_at_and_above_the_4096_character_cap_When_importing_strictly_Then_the_boundary_is_exact()
        {
            var filler = new string('a', 4096 - Abandon.Length);
            Assert.ThrowsAny<ArgumentException>(() => new MinimalHDWallet(Abandon + filler, ""));
            Assert.ThrowsAny<ArgumentException>(() => new MinimalHDWallet(Abandon, new string('p', 4097)));
            Assert.NotNull(new MinimalHDWallet(Abandon, new string('p', 4096)).Seed);
            var padded = Abandon + new string(' ', 4097 - Abandon.Length);
            Assert.True(SevenZeroSeed(padded, "").SequenceEqual(new MinimalHDWallet(padded, "", false).Seed));
            Assert.NotNull(new MinimalHDWallet(Abandon + new string(' ', 4096 - Abandon.Length), "", false).Seed);
        }

        [Trait("Rule", "S9-R2-47")]
        [Fact]
        public void Given_paths_at_the_depth_and_index_boundaries_When_deriving_Then_the_limits_are_exact()
        {
            var wallet = new MinimalHDWallet(Abandon);
            Assert.NotNull(wallet.GetKeyFromPath("m" + string.Concat(Enumerable.Repeat("/0'", 255))));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m" + string.Concat(Enumerable.Repeat("/0'", 256))));
            Assert.NotNull(wallet.GetKeyFromPath("m/2147483647'"));
            Assert.NotNull(wallet.GetKeyFromPath("m/2147483647"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/2147483648"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/2147483648'"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/99999999999999999999"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/0'/1'/"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/0 "));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/0\u0000"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/\uFF11"));
        }

        [Trait("Rule", "S9-R2-48")]
        [Fact]
        public void Given_the_strict_factory_When_loading_a_stored_vault_with_a_non_standard_mnemonic_Then_it_fails_and_the_lenient_default_loads_it()
        {
            var info = new MnemonicInfo("m", "ABANDON " + Abandon.Substring(8), "") { Id = "m1" };
            var json = JsonDocument.Parse("{\"type\":\"mnemonic\",\"address\":\"0x0\",\"label\":\"a\",\"index\":0,\"mnemonicId\":\"m1\"}").RootElement;
            var vault = new WalletVault(new Aes256GcmEncryptionStrategy());
            vault.AddMnemonic(info);
            Assert.NotNull(new MnemonicWalletAccountFactory().FromJson(json, vault));
            Assert.ThrowsAny<ArgumentException>(() => new MnemonicWalletAccountFactory(true).FromJson(json, vault));
        }
    }
}
