using Nethereum.Util;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class NativeTransferLogEmitterTests
    {
        private const string TokenContract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Fact]
        public void Given_TheSystemAddress_When_AskedIfItEmitsNativeTransferLogs_Then_ItDoes()
        {
            Assert.True(AddressUtil.SYSTEM_ADDRESS.IsNativeTransferLogEmitter());
        }

        [Fact]
        public void Given_AnOrdinaryContractAddress_When_AskedIfItEmitsNativeTransferLogs_Then_ItDoesNot()
        {
            Assert.False(TokenContract.IsNativeTransferLogEmitter());
        }

        [Fact]
        public void Given_TheSystemAddressInMixedCaseAndWithoutPrefix_When_AskedIfItEmitsNativeTransferLogs_Then_ItStillDoes()
        {
            Assert.True("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE".IsNativeTransferLogEmitter());
            Assert.True("fffffffffffffffffffffffffffffffffffffffe".IsNativeTransferLogEmitter());
        }

        [Fact]
        public void Given_NoAddress_When_AskedIfItEmitsNativeTransferLogs_Then_ItDoesNot()
        {
            Assert.False(((string)null).IsNativeTransferLogEmitter());
            Assert.False(string.Empty.IsNativeTransferLogEmitter());
        }

        [Fact]
        public void Given_TheBeaconRootsAddress_When_AskedIfItEmitsNativeTransferLogs_Then_ItDoesNotConfusedWithTheSystemAddress()
        {
            Assert.False("0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02".IsNativeTransferLogEmitter());
        }

        [Fact]
        public void Given_TheZeroAddress_When_AskedIfItEmitsNativeTransferLogs_Then_ItDoesNot()
        {
            Assert.False("0x0000000000000000000000000000000000000000".IsNativeTransferLogEmitter());
        }

    }
}
