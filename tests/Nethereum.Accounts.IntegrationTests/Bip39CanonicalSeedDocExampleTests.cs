using System.Security.Cryptography;
using System.Text;
using Nethereum.Accounts.Bip32;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    public class Bip39CanonicalSeedDocExampleTests
    {
        private const string Clean = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string Messy = " Abandon abandon  abandon abandon abandon abandon abandon abandon abandon abandon abandon about ";

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "A phrase with stray whitespace and capitals derives the same seed as the clean phrase")]
        public void Given_AMessyPhrase_When_TheSeedIsDerivedWithValidation_Then_ItEqualsTheCleanPhraseSeed()
        {
            var clean = Bip39.MnemonicToSeed(Clean);
            Assert.Equal(clean, Bip39.MnemonicToSeed(Messy));
            Assert.Equal(clean, Bip39.MnemonicToSeed(Messy, "", true));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "Without validation the seed is PBKDF2 over the raw NFKD phrase")]
        public void Given_AMessyPhrase_When_ValidationIsDisabled_Then_TheSeedIsTheRawNfkdDerivation()
        {
            var raw = Bip39.MnemonicToSeed(Messy, "", false);
            var expected = new Rfc2898DeriveBytes(
                Messy.Normalize(NormalizationForm.FormKD),
                Encoding.UTF8.GetBytes("mnemonic"),
                2048,
                HashAlgorithmName.SHA512).GetBytes(64);

            Assert.Equal(expected, raw);
            Assert.NotEqual(Bip39.MnemonicToSeed(Clean), raw);
            Assert.Equal(Bip39.MnemonicToSeed(Clean), Bip39.MnemonicToSeed(Clean, "", false));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "A messy phrase gives the same first address in MinimalHDWallet")]
        public void Given_AMessyPhrase_When_AMinimalHDWalletIsBuilt_Then_TheFirstAddressEqualsTheCleanPhraseAddress()
        {
            var clean = new MinimalHDWallet(Clean);
            var messy = new MinimalHDWallet(Messy);

            Assert.Equal("0x9858EfFD232B4033E47d90003D41EC34EcaEda94", clean.GetEthereumAddress(0));
            Assert.Equal(clean.GetEthereumAddress(0), messy.GetEthereumAddress(0));
        }
    }
}
