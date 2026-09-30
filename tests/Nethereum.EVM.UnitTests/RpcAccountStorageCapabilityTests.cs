using System.Linq;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class RpcAccountStorageCapabilityTests
    {
        private static string EmptyTrieRootHex => "0x" + DefaultValues.EMPTY_TRIE_HASH.ToHex();

        [Fact]
        public void Given_TheEmptyTrieRoot_When_Asked_Then_TheAccountHasNoStorage()
        {
            Assert.False(RpcNodeDataService.StorageHashIndicatesStorage(EmptyTrieRootHex));
        }

        [Fact]
        public void Given_AnyOtherStorageRoot_When_Asked_Then_TheAccountHasStorage()
        {
            var populated = "0x" + new string('a', 64);
            Assert.True(RpcNodeDataService.StorageHashIndicatesStorage(populated));
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void Given_TheEmptyTrieRootInAnyCasingOrPrefix_When_Asked_Then_ItStillReadsAsNoStorage(
            bool prefixed, bool upper)
        {
            var hex = DefaultValues.EMPTY_TRIE_HASH.ToHex();
            if (upper) hex = hex.ToUpperInvariant();
            if (prefixed) hex = "0x" + hex;

            Assert.False(RpcNodeDataService.StorageHashIndicatesStorage(hex),
                $"prefixed={prefixed} upper={upper} must still be recognised as the empty trie");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0x")]
        [InlineData("not-hex")]
        public void Given_NoUsableStorageHash_When_Asked_Then_ItDegradesToNoStorageRatherThanThrowing(
            string storageHash)
        {
            Assert.False(RpcNodeDataService.StorageHashIndicatesStorage(storageHash));
        }

    }
}
