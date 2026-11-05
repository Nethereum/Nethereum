using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Wallet.WalletAccounts;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9LegacySeedDetectionTests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string LegacyPhrase =
            "TRUE abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon FALSE";

        [Trait("Rule", "S9-BIP39-TRUE-FALSE")]
        [Theory]
        [InlineData("TRUE abandon abandon", true)]
        [InlineData("abandon abandon FALSE", true)]
        [InlineData("abandon  \tFALSE\n abandon", true)]
        [InlineData("TRUE FALSE", true)]
        [InlineData(Abandon, false)]
        [InlineData("true abandon false", false)]
        [InlineData("True abandon False", false)]
        [InlineData("TRUEISH abandon", false)]
        [InlineData("NOTTRUE abandon", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void Given_a_phrase_When_checking_for_the_7_0_uppercase_words_Then_only_exact_TRUE_or_FALSE_words_match(string phrase, bool expected)
        {
            Assert.Equal(expected, Bip39.IsLegacyUppercaseWordMnemonic(phrase));
        }

        [Trait("Rule", "S9-BIP39-TRUE-FALSE")]
        [Fact]
        public void Given_a_null_phrase_When_checking_Then_it_is_not_legacy()
        {
            Assert.False(Bip39.IsLegacyUppercaseWordMnemonic(null));
        }

        private static string SevenZeroAddress(string phrase)
        {
            var salt = Encoding.UTF8.GetBytes("mnemonic");
            using var derive = new Rfc2898DeriveBytes(phrase.Normalize(NormalizationForm.FormKD), salt, 2048, HashAlgorithmName.SHA512);
            return new MinimalHDWallet(derive.GetBytes(64)).GetEthereumAddress(0);
        }

        private static string BuildVaultBlob(string phrase, string address)
        {
            var strategy = new Aes256GcmEncryptionStrategy();
            var vault = new WalletVault(strategy);
            vault.AddMnemonic(new MnemonicInfo("m", phrase) { Id = "m1" });
            vault.Accounts.Add(new MnemonicWalletAccount(address, "a", 0, "m1", new MinimalHDWallet(phrase, "", false)));
            return vault.Encrypt("pw");
        }

        [Trait("Rule", "S9-BIP39-TRUE-FALSE")]
        [Fact]
        public async Task Given_a_vault_holding_a_7_0_phrase_with_uppercase_words_When_loading_Then_the_account_is_flagged_and_keeps_the_7_0_address()
        {
            var expected = SevenZeroAddress(LegacyPhrase);
            var blob = BuildVaultBlob(LegacyPhrase, expected);
            var vault = new WalletVault(new Aes256GcmEncryptionStrategy());
            vault.Decrypt(blob, "pw");

            var account = Assert.IsType<MnemonicWalletAccount>(vault.Accounts.Single());
            Assert.True(account.RequiresStandardSeedMigration);
            Assert.True(vault.Mnemonics.Single().RequiresStandardSeedMigration);
            Assert.Single(vault.AccountsWithLegacySeed);
            Assert.Single(vault.MnemonicsWithLegacySeed);
            Assert.Equal(expected, (await account.GetAccountAsync()).Address, true);
        }

        [Trait("Rule", "S9-BIP39-TRUE-FALSE")]
        [Fact]
        public void Given_a_clean_vault_When_loading_Then_nothing_is_flagged()
        {
            var blob = BuildVaultBlob(Abandon, new MinimalHDWallet(Abandon).GetEthereumAddress(0));
            var vault = new WalletVault(new Aes256GcmEncryptionStrategy());
            vault.Decrypt(blob, "pw");
            Assert.False(((MnemonicWalletAccount)vault.Accounts.Single()).RequiresStandardSeedMigration);
            Assert.Empty(vault.AccountsWithLegacySeed);
            Assert.Empty(vault.MnemonicsWithLegacySeed);
        }

        [Trait("Rule", "S9-BIP39-TRUE-FALSE")]
        [Fact]
        public async Task Given_a_service_unlocking_a_vault_with_a_7_0_phrase_When_reading_the_summary_Then_it_lists_the_flagged_accounts()
        {
            var service = new InMemoryWalletVaultService();
            await service.CreateNewAsync("pw");
            var vault = service.GetCurrentVault();
            vault.AddMnemonic(new MnemonicInfo("m", LegacyPhrase) { Id = "m1" });
            vault.Accounts.Add(new MnemonicWalletAccount(SevenZeroAddress(LegacyPhrase), "a", 0, "m1", new MinimalHDWallet(LegacyPhrase, "", false)));
            await service.SaveAsync();
            await service.LockAsync();
            Assert.False(service.HasAccountsWithLegacySeed);
            Assert.True(await service.UnlockAsync("pw"));
            Assert.True(service.HasAccountsWithLegacySeed);
            Assert.Single(service.AccountsWithLegacySeed);
        }
    }
}
