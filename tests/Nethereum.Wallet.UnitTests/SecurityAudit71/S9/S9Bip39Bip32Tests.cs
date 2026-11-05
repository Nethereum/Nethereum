using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NBitcoin;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using KeyPath = NBitcoin.KeyPath;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9Bip39Bip32Tests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string BadChecksum =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";
        private const string NotInWordList =
            "zzzzz abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        private static readonly string Vector2Seed =
            "fffcf9f6f3f0edeae7e4e1dedbd8d5d2cfccc9c6c3c0bdbab7b4b1aeaba8a5a29f9c999693908d8a8784817e7b7875726f6c696663605d5a5754514e4b484542";
        private static readonly string Vector3Seed =
            "4b381541583be4423346c643850da4b320e46a87ae3d2a4e6da11eba819cd4acba45d239319ac14f863b8d5ab5a0d0c64d2e8a1e7d1457df2e5a3c51c73235be";

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_a_mnemonic_with_a_bad_checksum_When_creating_a_MinimalHDWallet_Then_it_is_rejected()
        {
            Assert.ThrowsAny<Exception>(() => new MinimalHDWallet(BadChecksum));
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_a_mnemonic_with_a_word_outside_the_word_list_When_creating_a_MinimalHDWallet_Then_it_is_rejected()
        {
            Assert.ThrowsAny<Exception>(() => new MinimalHDWallet(NotInWordList));
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_a_mnemonic_with_the_wrong_word_count_When_creating_a_MinimalHDWallet_Then_it_is_rejected()
        {
            Assert.ThrowsAny<Exception>(() => new MinimalHDWallet("abandon abandon abandon"));
        }

        [Trait("Rule", "S9-22")]
        [Fact]
        public void Given_a_sixteen_megabyte_mnemonic_When_creating_a_MinimalHDWallet_Then_it_is_rejected_quickly()
        {
            var huge = string.Join(" ", Enumerable.Repeat("abandon", 2_000_000));
            var sw = Stopwatch.StartNew();
            var rejected = false;
            try { new MinimalHDWallet(huge); } catch (Exception) { rejected = true; }
            Assert.True(rejected, "accepted a " + huge.Length + " char mnemonic in " + sw.Elapsed);
        }

        [Trait("Rule", "S9-22")]
        [Fact]
        public void Given_a_sixteen_megabyte_passphrase_When_creating_a_MinimalHDWallet_Then_it_is_rejected_quickly()
        {
            var huge = new string('a', 16 * 1024 * 1024);
            var sw = Stopwatch.StartNew();
            var rejected = false;
            try { new MinimalHDWallet(Abandon, huge); } catch (Exception) { rejected = true; }
            Assert.True(rejected, "accepted a " + huge.Length + " char passphrase in " + sw.Elapsed);
        }

        [Trait("Rule", "S9-23")]
        [Fact]
        public void Given_the_bip39_trezor_vector_When_deriving_the_seed_Then_it_matches_and_nfc_equals_nfkd_passphrase()
        {
            var seed = new MinimalHDWallet(Abandon, "TREZOR").Seed.ToHex();
            Assert.Equal(
                "c55257c360c07c72029aebc1b53c05ed0362ada38ead3e3e9efa3708e53495531f09a6987599d18264c1e1c92f2cf141630c7a3c4ab7c81b2f001698e7463b04",
                seed);
            var composed = "caféÅΩ";
            var decomposed = composed.Normalize(NormalizationForm.FormKD);
            Assert.NotEqual(composed, decomposed);
            Assert.Equal(new MinimalHDWallet(Abandon, composed).Seed.ToHex(), new MinimalHDWallet(Abandon, decomposed).Seed.ToHex());
            var nbitcoin = new Mnemonic(Abandon).DeriveSeed(composed).ToHex();
            Assert.Equal(nbitcoin, new MinimalHDWallet(Abandon, composed).Seed.ToHex());
        }

        [Trait("Rule", "S9-24")]
        [Fact]
        public void Given_the_hardened_index_2_pow_31_When_deriving_a_path_Then_it_does_not_alias_index_0()
        {
            var wallet = new MinimalHDWallet(Abandon);
            var alias = Try(() => wallet.GetKeyFromPath("m/2147483648'").GetPrivateKey());
            var zero = wallet.GetKeyFromPath("m/0'").GetPrivateKey();
            Assert.True(alias == null || alias != zero, "m/2147483648' derived the same key as m/0'");
        }

        [Trait("Rule", "S9-24")]
        [Fact]
        public void Given_a_non_hardened_index_at_or_above_2_pow_31_When_deriving_a_path_Then_it_is_rejected_or_equals_the_hardened_derivation()
        {
            var wallet = new MinimalHDWallet(Abandon);
            var nonHardened = Try(() => wallet.GetKeyFromPath("m/2147483648").GetPrivateKey());
            var hardened = wallet.GetKeyFromPath("m/0'").GetPrivateKey();
            Assert.True(nonHardened == null || nonHardened == hardened,
                "m/2147483648 derived a key that matches neither a rejection nor m/0'");
        }

        [Trait("Rule", "S9-24")]
        [Fact]
        public void Given_malformed_path_segments_When_deriving_Then_every_one_is_rejected_with_an_ArgumentException()
        {
            var wallet = new MinimalHDWallet(Abandon);
            var accepted = new List<string>();
            foreach (var path in new[]
            {
                "", "x", "M/0", "0/0", "m/", "m//0", "m/0/", "m/x", "m/-1", "m/4294967296", "m/ 5", "m/5 ",
                "m/+5", "m/5''", "m/'", "m/0'/'", "m/1e3", "m/0x10", "m/٥", "m/5\n", "m/00005"
            })
            {
                try
                {
                    wallet.GetKeyFromPath(path);
                    accepted.Add("'" + path.Replace("\n", "\\n") + "'");
                }
                catch (ArgumentException) { }
            }
            Assert.True(accepted.Count == 0, "accepted: " + string.Join(", ", accepted));
        }

        [Trait("Rule", "S9-24")]
        [Fact]
        public void Given_a_null_path_When_deriving_Then_it_throws_ArgumentNullException()
        {
            var wallet = new MinimalHDWallet(Abandon);
            Assert.Throws<ArgumentNullException>(() => wallet.GetKeyFromPath(null));
        }

        [Trait("Rule", "S9-25")]
        [Fact]
        public void Given_a_path_with_a_thousand_segments_When_deriving_Then_it_is_rejected_beyond_the_bip32_depth_of_255()
        {
            var wallet = new MinimalHDWallet(Abandon);
            var path = "m" + string.Concat(Enumerable.Repeat("/0'", 1000));
            Assert.ThrowsAny<Exception>(() => wallet.GetKeyFromPath(path));
        }

        [Trait("Rule", "S9-26")]
        [Fact]
        public void Given_the_bip32_vector_2_and_3_seeds_When_deriving_Then_every_path_matches_the_independent_implementation()
        {
            var mismatches = new List<string>();
            var paths = new[]
            {
                "m", "m/0", "m/0'", "m/0/2147483647'", "m/0/2147483647'/1", "m/0/2147483647'/1/2147483646'",
                "m/0/2147483647'/1/2147483646'/2", "m/44'/60'/0'/0/0", "m/44'/60'/0'/0/1", "m/1'/2'/3'/4'/5'"
            };
            foreach (var seedHex in new[] { Vector2Seed, Vector3Seed })
            {
                var minimal = new MinimalHDWallet(seedHex.HexToByteArray());
                var master = new ExtKey(seedHex);
                foreach (var path in paths)
                {
                    var expected = (path == "m" ? master : master.Derive(new KeyPath(path))).PrivateKey.ToBytes().ToHex();
                    var actual = minimal.GetKeyFromPath(path).GetPrivateKeyAsBytes().ToHex();
                    if (expected != actual) mismatches.Add(seedHex.Substring(0, 8) + " " + path);
                }
            }
            Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
        }

        [Trait("Rule", "S9-26")]
        [Fact]
        public void Given_the_bip32_vector_2_master_and_first_child_When_deriving_Then_they_match_the_published_keys()
        {
            var minimal = new MinimalHDWallet(Vector2Seed.HexToByteArray());
            Assert.Equal("4b03d6fc340455b363f51020ad3ecca4f0850280cf436c70c727923f6db46c3e",
                minimal.GetKeyFromPath("m").GetPrivateKeyAsBytes().ToHex());
            Assert.Equal("abe74a98f6c7eabee0428f53798f0ab8aa1bd37873999041703c742f15ac7e1e",
                minimal.GetKeyFromPath("m/0").GetPrivateKeyAsBytes().ToHex());
        }

        [Trait("Rule", "S9-26")]
        [Fact]
        public void Given_the_bip32_vector_3_leading_zero_master_When_deriving_Then_the_key_keeps_its_leading_zero_byte()
        {
            var minimal = new MinimalHDWallet(Vector3Seed.HexToByteArray());
            Assert.Equal("00ddb80b067e0d4993197fe10f2657a844a384589847602d56f0c629c81aae32",
                minimal.GetKeyFromPath("m").GetPrivateKeyAsBytes().ToHex());
            Assert.Equal("491f7a2eebc7b57028e0d3faa0acda02e75c33b03c48fb288c41e2ea44e1daef",
                minimal.GetKeyFromPath("m/0'").GetPrivateKeyAsBytes().ToHex());
        }

        [Trait("Rule", "S9-26")]
        [Fact]
        public void Given_the_abandon_about_mnemonic_When_deriving_the_default_ethereum_path_Then_the_address_is_the_published_one()
        {
            Assert.Equal("0x9858EfFD232B4033E47d90003D41EC34EcaEda94", new MinimalHDWallet(Abandon).GetEthereumAddress(0));
        }

        private static string Try(Func<string> derive)
        {
            try { return derive(); }
            catch (Exception) { return null; }
        }
    }
}
