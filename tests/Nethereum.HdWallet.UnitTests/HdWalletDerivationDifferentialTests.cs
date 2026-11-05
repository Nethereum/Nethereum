using System.Collections.Generic;
using NBitcoin;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.HdWallet.UnitTests
{
    public class HdWalletDerivationDifferentialTests
    {
        private const int Indexes = 25;

        private static readonly string[] Passwords = { "", "TREZOR", "a much longer seed password 0123456789" };

        public static IEnumerable<object[]> Paths()
        {
            yield return new object[] { Wallet.DEFAULT_PATH };
            yield return new object[] { Wallet.ELECTRUM_LEDGER_PATH };
            yield return new object[] { "m/44'/60'/1'/0/x" };
            yield return new object[] { "m/44'/60'/0'/1/x" };
        }

        private static IEnumerable<string> Mnemonics()
        {
            yield return WalletTests.Words;
            yield return "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            foreach (var count in new[] { WordCount.Twelve, WordCount.Fifteen, WordCount.Eighteen, WordCount.TwentyOne, WordCount.TwentyFour })
                yield return string.Join(" ", new Wallet(Wordlist.English, count).Words);
        }

        private static string MasterPath(string path) => path.Replace("/x", "");

        [Theory]
        [MemberData(nameof(Paths))]
        public void Given_AMnemonicAndPassword_When_DerivingAccountsOnAPath_Then_EveryAddressEqualsTheIndependentBip32Derivation(string path)
        {
            foreach (var words in Mnemonics())
            foreach (var password in Passwords)
            {
                var wallet = new Wallet(words, password, path);
                var reference = new MinimalHDWallet(words, password);
                for (var i = 0; i < Indexes; i++)
                    Assert.Equal(
                        reference.GetKeyFromPath(path.Replace("x", i.ToString())).GetPublicAddress(),
                        wallet.GetAccount(i).Address);
            }
        }

        [Theory]
        [MemberData(nameof(Paths))]
        public void Given_AMnemonic_When_DerivingHardenedChildren_Then_EachKeyEqualsTheIndependentHardenedDerivation(string path)
        {
            foreach (var words in Mnemonics())
            {
                var wallet = new Wallet(words, WalletTests.Password, path);
                var reference = new MinimalHDWallet(words, WalletTests.Password);
                for (var i = 0; i < Indexes; i++)
                    Assert.Equal(
                        reference.GetKeyFromPath(MasterPath(path) + "/" + i + "'").GetPrivateKeyAsBytes(),
                        wallet.GetExtKey(i, true).PrivateKey.ToBytes());
            }
        }

        [Theory]
        [MemberData(nameof(Paths))]
        public void Given_TheMasterPublicWallet_When_DerivingAddresses_Then_TheyEqualThePrivateDerivation(string path)
        {
            foreach (var words in Mnemonics())
            {
                var wallet = new Wallet(words, WalletTests.Password, path);
                var publicWallet = wallet.GetMasterPublicWallet();
                var masterExtPubKey = wallet.GetMasterExtPubKey();
                var fromXpub = new PublicWallet(ExtPubKey.Parse(masterExtPubKey.ToString(Network.Main), Network.Main));
                var fromBytes = new PublicWallet(masterExtPubKey.ToBytes());
                var fromHex = new PublicWallet(masterExtPubKey.ToBytes().ToHex());
                for (var i = 0; i < Indexes; i++)
                {
                    var expected = wallet.GetAccount(i).Address;
                    Assert.Equal(expected, publicWallet.GetAddress(i));
                    Assert.Equal(expected, fromXpub.GetAddress(i));
                    Assert.Equal(expected, fromBytes.GetAddress(i));
                    Assert.Equal(expected, fromHex.GetAddress(i));
                }
            }
        }

        [Fact]
        public void Given_AChildPublicWallet_When_DerivingGrandchildAddresses_Then_TheyEqualTheIndependentDerivation()
        {
            foreach (var words in Mnemonics())
            {
                var wallet = new Wallet(words, WalletTests.Password, Wallet.ELECTRUM_LEDGER_PATH);
                var reference = new MinimalHDWallet(words, WalletTests.Password);
                for (var child = 0; child < 5; child++)
                {
                    var childWallet = wallet.GetMasterPublicWallet().GetChildPublicWallet(child);
                    for (var i = 0; i < 5; i++)
                        Assert.Equal(
                            reference.GetKeyFromPath("m/44'/60'/0'/" + child + "/" + i).GetPublicAddress(),
                            childWallet.GetAddress(i));
                }
            }
        }

        [Fact]
        public void Given_TheAbandonAboutMnemonic_When_DerivingTheLedgerLivePathAndTheDefaultPath_Then_TheyDiffer()
        {
            const string words = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var standard = new Wallet(words, "", Wallet.DEFAULT_PATH);
            var electrum = new Wallet(words, "", Wallet.ELECTRUM_LEDGER_PATH);

            Assert.Equal("0x9858EfFD232B4033E47d90003D41EC34EcaEda94", standard.GetAccount(0).Address);
            Assert.NotEqual(standard.GetAccount(0).Address, electrum.GetAccount(0).Address);
        }
    }
}
