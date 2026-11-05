using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class TransactionExecutionContextAddressTests
    {
        private const string First = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string Second = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        [Fact]
        public void Given_ASenderToAndCoinbase_When_TheirAddressesAreRead_Then_TheyAreTheParsedStrings()
        {
            var ctx = new TransactionExecutionContext { Sender = First, To = Second, Coinbase = First.ToLowerInvariant() };

            Assert.Equal(EvmAddress.FromHex(First), ctx.SenderAddress);
            Assert.Equal(EvmAddress.FromHex(Second), ctx.ToAddress);
            Assert.Equal(EvmAddress.FromHex(First), ctx.CoinbaseAddress);
        }

        [Fact]
        public void Given_AddressesAlreadyRead_When_TheStringsAreReassigned_Then_TheAddressesFollow()
        {
            var ctx = new TransactionExecutionContext { Sender = First, To = First, Coinbase = First };
            Assert.Equal(EvmAddress.FromHex(First), ctx.SenderAddress);
            Assert.Equal(EvmAddress.FromHex(First), ctx.ToAddress);
            Assert.Equal(EvmAddress.FromHex(First), ctx.CoinbaseAddress);

            ctx.Sender = Second;
            ctx.To = Second;
            ctx.Coinbase = Second;

            Assert.Equal(EvmAddress.FromHex(Second), ctx.SenderAddress);
            Assert.Equal(EvmAddress.FromHex(Second), ctx.ToAddress);
            Assert.Equal(EvmAddress.FromHex(Second), ctx.CoinbaseAddress);
        }

        [Fact]
        public void Given_NoRecipient_When_ToAddressIsRead_Then_ItIsTheZeroAddress()
        {
            Assert.Equal(EvmAddress.Zero, new TransactionExecutionContext().ToAddress);
        }
    }
}
