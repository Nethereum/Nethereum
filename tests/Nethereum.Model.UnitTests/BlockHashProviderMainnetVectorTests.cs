using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class BlockHashProviderMainnetVectorTests
    {
        private const string MainnetGenesisHash = "d4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3";

        private static BlockHeader MainnetGenesisHeader() => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = DefaultValues.EMPTY_UNCLES_HASH,
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = "d7f8974fb5ac78d9ac099b9ad5018bedc2ce0a72dad1827a1709da30580f0544".HexToByteArray(),
            TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
            ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
            LogsBloom = new byte[256],
            Difficulty = new EvmUInt256(17179869184UL),
            BlockNumber = new EvmUInt256(0UL),
            GasLimit = 5000,
            GasUsed = 0,
            Timestamp = 0,
            ExtraData = "11bbe8db4e347b4e8c937c1c8370e4b5ed33adb3db69cbdb7a38e1e50b1b82fa".HexToByteArray(),
            MixHash = new byte[32],
            Nonce = "0000000000000042".HexToByteArray()
        };

        [Fact]
        public void Given_AnyHeader_When_HashedByRlpKeccakProvider_Then_ItEqualsKeccakOfTheEncodedHeader()
        {
            var header = MainnetGenesisHeader();

            var hash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);

            Assert.Equal(new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header)), hash);
            Assert.Equal(MainnetGenesisHash, hash.ToHex());
        }

        [Fact]
        public void Given_ALondonShapedHeader_When_HashedByRlpKeccakProvider_Then_ItEqualsKeccakOfTheEncodedHeader()
        {
            var header = BlockHeaderShapes.For("london");

            Assert.Equal(
                new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header)),
                RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header));
        }
    }
}
