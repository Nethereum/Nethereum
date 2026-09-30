using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class FinalityLabelledUncleHashTests
    {
        private static byte[] HandRolledUncleHash(BlockHeader uncle)
            => Sha3Keccack.Current.CalculateHash(BlockHeaderEncoder.Current.Encode(uncle));

        [Fact]
        public void Given_UncleHeader_When_ComputedViaBlockHashCalculator_Then_MatchesHandRolledKeccakOfEncodedHeader()
        {
            var uncle = new BlockHeader
            {
                BlockNumber = 5,
                Timestamp = 123,
                Difficulty = new EvmUInt256(1000UL),
                ParentHash = new byte[32],
                Coinbase = "0x000000000000000000000000000000000000dead"
            };

            var expected = HandRolledUncleHash(uncle);
            var actual = BlockHashCalculator.ForHeader(uncle);

            Assert.Equal(expected, actual);
            Assert.Equal(expected.ToHex(true), actual.ToHex(true));
        }

        [Fact]
        public void Given_DifferentUncleHeaders_When_ComputedViaBlockHashCalculator_Then_HashesDiffer()
        {
            var uncleA = new BlockHeader { BlockNumber = 1, Timestamp = 1, Difficulty = new EvmUInt256(1UL) };
            var uncleB = new BlockHeader { BlockNumber = 2, Timestamp = 1, Difficulty = new EvmUInt256(1UL) };

            Assert.NotEqual(BlockHashCalculator.ForHeader(uncleA), BlockHashCalculator.ForHeader(uncleB));
        }
    }
}
