using System;
using System.Linq;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Wallet.WalletAccounts;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9MnemonicValidationTests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string BadChecksum =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";
        private const string TwentyFour =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art";

        [Trait("Rule", "S9-20")]
        [Theory]
        [InlineData(12)]
        [InlineData(15)]
        [InlineData(18)]
        [InlineData(21)]
        [InlineData(24)]
        public void Given_a_generated_mnemonic_of_each_standard_length_When_creating_a_MinimalHDWallet_Then_it_is_accepted(int words)
        {
            var mnemonic = Bip39.GenerateMnemonic(words);
            Assert.Equal(words, mnemonic.Split(' ').Length);
            Assert.NotNull(new MinimalHDWallet(mnemonic).Seed);
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_the_bip39_word_list_When_compared_with_the_nbitcoin_english_list_Then_every_word_matches_in_order()
        {
            var expected = NBitcoin.Wordlist.English.GetWords();
            Assert.Equal(expected, Bip39.WordList);
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_thousands_of_generated_mnemonics_When_validating_Then_every_one_is_valid_and_agrees_with_nbitcoin()
        {
            var rejected = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 3000; i++)
            {
                var mnemonic = Bip39.GenerateMnemonic(i % 2 == 0 ? 12 : 24);
                if (!Bip39.IsValidMnemonic(mnemonic) || !new NBitcoin.Mnemonic(mnemonic).IsValidChecksum) rejected.Add(mnemonic);
            }
            Assert.True(rejected.Count == 0, string.Join(" | ", rejected.Take(3)));
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_the_published_24_word_vector_When_creating_a_MinimalHDWallet_Then_it_is_accepted()
        {
            Assert.Equal(64, new MinimalHDWallet(TwentyFour).Seed.Length);
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_a_bad_checksum_When_creating_a_MinimalHDWallet_Then_an_ArgumentException_is_thrown()
        {
            Assert.Throws<ArgumentException>(() => new MinimalHDWallet(BadChecksum));
            Assert.Throws<ArgumentException>(() => new MinimalHDWallet("abandon abandon abandon"));
            Assert.Throws<ArgumentException>(() => new MinimalHDWallet("zzzzz " + string.Join(" ", Enumerable.Repeat("abandon", 10)) + " about"));
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_extra_whitespace_between_words_When_creating_a_MinimalHDWallet_Then_the_seed_is_the_canonical_bip39_seed()
        {
            var spaced = "  abandon\tabandon  abandon abandon abandon abandon abandon abandon abandon abandon abandon\nabout ";
            Assert.Equal(new MinimalHDWallet(Abandon).Seed.ToHex(), new MinimalHDWallet(spaced).Seed.ToHex());
            Assert.NotEqual(Independent7Seed(spaced), new MinimalHDWallet(spaced).Seed.ToHex());
            Assert.Equal(Independent7Seed(spaced), new MinimalHDWallet(spaced, "", false).Seed.ToHex());
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_validation_is_turned_off_When_creating_a_MinimalHDWallet_from_a_non_standard_mnemonic_Then_the_legacy_seed_is_derived()
        {
            var wallet = new MinimalHDWallet(BadChecksum, "", false);
            Assert.Equal(Bip39.MnemonicToSeed(BadChecksum, "", false).ToHex(), wallet.Seed.ToHex());
            Assert.Equal(64, new MinimalHDWallet("not a real mnemonic", "pw", false).Seed.Length);
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_the_default_mnemonic_account_factory_When_the_stored_mnemonic_is_non_standard_Then_the_vault_still_loads()
        {
            var vault = new WalletVault(new Aes256GcmEncryptionStrategy());
            vault.AddMnemonic(new MnemonicInfo("legacy", BadChecksum) { Id = "m1" });
            var json = System.Text.Json.JsonDocument.Parse(
                "{\"type\":\"mnemonic\",\"address\":\"0x1\",\"label\":\"x\",\"index\":0,\"mnemonicId\":\"m1\"}").RootElement;
            Assert.NotNull(new MnemonicWalletAccountFactory().FromJson(json, vault));
            Assert.ThrowsAny<Exception>(() => new MnemonicWalletAccountFactory(true).FromJson(json, vault));
        }

        private static string Independent7Seed(string mnemonic)
        {
            var salt = System.Text.Encoding.UTF8.GetBytes("mnemonic".Normalize(System.Text.NormalizationForm.FormKD));
            using var derive = new System.Security.Cryptography.Rfc2898DeriveBytes(
                mnemonic.Normalize(System.Text.NormalizationForm.FormKD), salt, 2048, System.Security.Cryptography.HashAlgorithmName.SHA512);
            return derive.GetBytes(64).ToHex();
        }

        [Trait("Rule", "S9-20")]
        [Theory]
        [InlineData("ABANDON ABANDON ABANDON ABANDON ABANDON ABANDON ABANDON ABANDON ABANDON ABANDON ABANDON ABOUT")]
        [InlineData("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon  about ")]
        public async System.Threading.Tasks.Task Given_a_mnemonic_accepted_by_7_0_stored_in_a_vault_When_unlocking_Then_it_loads_and_derives_the_7_0_address(string stored)
        {
            var seedKey = new MinimalHDWallet(Independent7Seed(stored).HexToByteArray());
            var expected = seedKey.GetEthereumAddress(0);
            var writer = new WalletVault(new Aes256GcmEncryptionStrategy());
            writer.AddMnemonic(new MnemonicInfo("legacy", stored) { Id = "m1" });
            writer.Accounts.Add(new MnemonicWalletAccount(expected, "a0", 0, "m1", new MinimalHDWallet(stored, "", false)));
            var blob = writer.Encrypt("pw");

            var reader = new WalletVault(new Aes256GcmEncryptionStrategy());
            reader.Decrypt(blob, "pw");
            var account = await reader.Accounts.Single().GetAccountAsync();
            Assert.Equal(expected, account.Address, true);
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_an_uppercase_mnemonic_accepted_by_7_0_When_importing_it_strictly_Then_it_derives_the_canonical_seed_and_the_lenient_path_derives_the_7_0_seed()
        {
            var upper = Abandon.ToUpperInvariant();
            Assert.Equal(new MinimalHDWallet(Abandon).Seed.ToHex(), new MinimalHDWallet(upper).Seed.ToHex());
            Assert.NotEqual(Independent7Seed(upper), new MinimalHDWallet(upper).Seed.ToHex());
            Assert.Equal(Independent7Seed(upper), new MinimalHDWallet(upper, "", false).Seed.ToHex());
        }

        [Trait("Rule", "S9-22")]
        [Fact]
        public void Given_a_mnemonic_or_passphrase_longer_than_4096_characters_When_creating_a_MinimalHDWallet_Then_an_ArgumentException_is_thrown()
        {
            Assert.Throws<ArgumentException>(() => new MinimalHDWallet(new string('a', 4097)));
            Assert.Throws<ArgumentException>(() => new MinimalHDWallet(Abandon, new string('a', 4097)));
            Assert.NotNull(new MinimalHDWallet(new string('a', 4097), "", false).Seed);
            Assert.NotNull(new MinimalHDWallet(Abandon, new string('a', 4096)));
        }

        [Trait("Rule", "S9-25")]
        [Fact]
        public void Given_paths_at_and_beyond_the_maximum_depth_When_deriving_Then_255_levels_work_and_256_are_rejected()
        {
            var wallet = new MinimalHDWallet(Abandon);
            Assert.NotNull(wallet.GetKeyFromPath("m" + string.Concat(Enumerable.Repeat("/0", 255))));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m" + string.Concat(Enumerable.Repeat("/0", 256))));
        }

        [Trait("Rule", "S9-24")]
        [Fact]
        public void Given_the_largest_valid_indices_When_deriving_Then_they_are_accepted_and_one_more_is_rejected()
        {
            var wallet = new MinimalHDWallet(Abandon);
            Assert.NotNull(wallet.GetKeyFromPath("m/2147483647"));
            Assert.NotNull(wallet.GetKeyFromPath("m/2147483647'"));
            Assert.NotNull(wallet.GetKeyFromPath("m/0/0'/0"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/2147483648"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/2147483648'"));
        }
    }
}
