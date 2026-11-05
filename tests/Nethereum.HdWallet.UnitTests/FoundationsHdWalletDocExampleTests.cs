using System;
using Nethereum.Documentation;
using Nethereum.HdWallet;
using Xunit;

namespace Nethereum.HdWallet.UnitTests
{
    public class FoundationsHdWalletDocExampleTests
    {
        private const string Words = "ripple scissors kick mammal hire column oak again sun offer wealth tomorrow wagon turn fatal";
        private const string WrongChecksum = "ripple scissors kick mammal hire column oak again sun offer wealth tomorrow wagon turn turn";

        [Fact]
        [NethereumDocExample(DocSection.Signing, "hd-wallets", "A mnemonic is validated by default and the lenient overload is opt-in", Order = 5)]
        public void Given_AMnemonicWithABadChecksum_When_ARestoredWalletIsCreated_Then_ItIsRefusedUnlessValidationIsTurnedOff()
        {
            Assert.Throws<ArgumentException>(() => new Wallet(WrongChecksum, "TREZOR"));
            Assert.Throws<ArgumentException>(() => new Wallet("not a mnemonic", null));

            var lenient = new Wallet(WrongChecksum, "TREZOR", Wallet.DEFAULT_PATH, null, false);

            Assert.False(lenient.IsMnemonicValidChecksum);
            Assert.True(new Wallet(Words, "TREZOR").IsMnemonicValidChecksum);
        }
    }
}
