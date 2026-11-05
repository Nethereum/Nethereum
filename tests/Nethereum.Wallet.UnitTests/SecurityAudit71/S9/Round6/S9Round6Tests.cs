using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Accounts.Bip32;
using Nethereum.Wallet.WalletAccounts;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9.Round6
{
    public class S9Round6Tests
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

        [Trait("Rule", "S9-R6-1")]
        [Fact]
        public void Given_very_long_stored_phrases_and_passphrases_When_loading_leniently_Then_the_seed_equals_the_7_0_seed_and_the_flag_is_set()
        {
            foreach (var length in new[] { 4097, 100_000, 3_000_000 })
            {
                var phrase = Abandon + new string(' ', length);
                var passphrase = new string('p', length);
                Assert.True(SevenZeroSeed(phrase, passphrase).SequenceEqual(new MinimalHDWallet(phrase, passphrase, false).Seed), "length " + length);
                Assert.True(Bip39.IsNonStandardMnemonicForm(phrase));
                Assert.True(new MnemonicInfo("l", phrase).RequiresStandardSeedMigration);
                Assert.False(Bip39.IsNonStandardMnemonicForm(Abandon));
            }
        }

        [Trait("Rule", "S9-R6-2")]
        [Fact]
        public void Given_the_same_long_inputs_When_importing_strictly_Then_they_are_still_rejected()
        {
            var phrase = Abandon + new string(' ', 4097);
            Assert.ThrowsAny<ArgumentException>(() => new MinimalHDWallet(phrase, ""));
            Assert.ThrowsAny<ArgumentException>(() => new MinimalHDWallet(Abandon, new string('p', 4097)));
            Assert.ThrowsAny<ArgumentException>(() => Bip39.NormalizeMnemonic(phrase));
            Assert.ThrowsAny<ArgumentException>(() => Bip39.CanonicalMnemonic(phrase));
            Assert.ThrowsAny<ArgumentException>(() => Bip39.MnemonicToSeed(phrase, "", true));
            var service = new CoreWalletAccountService(new WalletVault(new Aes256GcmEncryptionStrategy()));
            Assert.ThrowsAny<Exception>(() => service.CreateMnemonicAccountAsync(phrase).GetAwaiter().GetResult());
            Assert.False(Bip39.IsValidMnemonic(phrase));
        }

        [Trait("Rule", "S9-R6-3")]
        [Fact]
        public void Given_a_gcm_vault_storing_a_phrase_and_a_passphrase_over_4096_characters_When_decrypting_Then_it_loads_with_the_7_0_address()
        {
            var phrase = Abandon + new string('\n', 100_000);
            var passphrase = new string('p', 50_000);
            var expected = new MinimalHDWallet(SevenZeroSeed(phrase, passphrase)).GetEthereumAddress(0);
            var json = "{\"mnemonics\":[{\"label\":\"m\",\"mnemonic\":" + JsonSerializer.Serialize(phrase) + ",\"passphrase\":" + JsonSerializer.Serialize(passphrase) +
                       ",\"id\":\"m1\"}],\"accounts\":[{\"type\":\"mnemonic\",\"address\":\"" + expected + "\",\"label\":\"a\",\"index\":0,\"mnemonicId\":\"m1\"}],\"hardwareDevices\":[]}";
            var strategy = new Aes256GcmEncryptionStrategy();
            var vault = new WalletVault(strategy);
            vault.Decrypt(Convert.ToBase64String(strategy.Encrypt(Encoding.UTF8.GetBytes(json), "pw")), "pw");
            Assert.Equal(expected, vault.Accounts.Single().GetAccountAsync().GetAwaiter().GetResult().Address, ignoreCase: true);
            Assert.True(vault.AccountsWithLegacySeed.Count == 1);
            Assert.True(vault.IsConsistentAsync().GetAwaiter().GetResult());
        }

        [Trait("Rule", "S9-R6-3")]
        [Fact]
        public async System.Threading.Tasks.Task Given_a_legacy_cbc_vault_with_an_over_long_phrase_When_unlocking_Then_it_unlocks_migrates_and_unlocks_again()
        {
            var phrase = Abandon + new string('\n', 20_000);
            var expected = new MinimalHDWallet(SevenZeroSeed(phrase, "")).GetEthereumAddress(0);
            var json = "{\"mnemonics\":[{\"label\":\"m\",\"mnemonic\":" + JsonSerializer.Serialize(phrase) + ",\"passphrase\":\"\",\"id\":\"m1\"}],\"accounts\":[{\"type\":\"mnemonic\",\"address\":\"" +
                       expected + "\",\"label\":\"a\",\"index\":0,\"mnemonicId\":\"m1\"}],\"hardwareDevices\":[]}";
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "s9r6-" + Guid.NewGuid().ToString("N") + ".vault");
            try
            {
                var key = new Rfc2898DeriveBytes("pw", Encoding.UTF8.GetBytes("NethereumWallet16"), 10000, HashAlgorithmName.SHA256).GetBytes(32);
                using var aes = Aes.Create();
                aes.Key = key;
                aes.GenerateIV();
                var cipher = aes.EncryptCbc(Encoding.UTF8.GetBytes(json), aes.IV);
                await System.IO.File.WriteAllTextAsync(path, Convert.ToBase64String(aes.IV.Concat(cipher).ToArray()));
                var first = new FileWalletVaultService(path);
                Assert.True(await first.UnlockAsync("pw"));
                Assert.True(Aes256GcmEncryptionStrategy.IsCurrentFormat(Convert.FromBase64String(System.IO.File.ReadAllText(path))));
                var second = new FileWalletVaultService(path);
                Assert.True(await second.UnlockAsync("pw"));
                Assert.True(second.HasAccountsWithLegacySeed);
                Assert.Equal(expected, (await second.GetCurrentVault().Accounts.Single().GetAccountAsync()).Address, ignoreCase: true);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }
    }
}
