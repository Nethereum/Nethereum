using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Codecs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class RlpKeccakBlockHashProviderTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private static string HashOf(BlockHeader header)
            => RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header).ToHex();

        private static string HashUnder(IBlockHeaderCodec codec, BlockHeader header)
            => Keccak.CalculateHash(codec.Encode(header)).ToHex();

        public static TheoryData<string, string> AmsterdamHeadersMissingOneField =>
            new TheoryData<string, string>
            {
                { "blockAccessListHash", "without a slot number" },
                { "slotNumber", "without a block access list hash" }
            };

        /// <summary>
        /// EIP-7928 adds <c>blockAccessListHash</c> and EIP-7843 adds
        /// <c>slotNumber</c>; both are unconditional Amsterdam header fields, so
        /// a header carrying one without the other is a shape no fork emits.
        /// </summary>
        [Theory]
        [MemberData(nameof(AmsterdamHeadersMissingOneField))]
        public void Given_AnAmsterdamHeaderMissingOneNewField_When_Hashed_Then_ItFailsRatherThanHashingAsPrague(
            string fieldPresent, string expectedComplaint)
        {
            var header = BlockHeaderShapes.For("prague");
            if (fieldPresent == "blockAccessListHash") header.BlockAccessListHash = BlockHeaderShapes.Bytes(0xEE);
            else header.SlotNumber = 9_876_543;

            Assert.Equal(
                HashUnder(PragueBlockHeaderCodec.Instance, BlockHeaderShapes.For("prague")),
                HashUnder(PragueBlockHeaderCodec.Instance, header));

            var refusal = Assert.Throws<ArgumentException>(() => HashOf(header));
            Assert.Contains(expectedComplaint, refusal.Message);
        }

        [Fact]
        public void Given_AGenuinePragueHeader_When_Hashed_Then_ItStillUsesThePragueCodec()
        {
            var header = BlockHeaderShapes.For("prague");

            Assert.Equal(HashUnder(PragueBlockHeaderCodec.Instance, header), HashOf(header));
        }

        [Fact]
        public void Given_AGenuineAmsterdamHeader_When_Hashed_Then_ItStillUsesTheAmsterdamCodec()
        {
            var header = BlockHeaderShapes.For("amsterdam");

            Assert.Equal(HashUnder(AmsterdamBlockHeaderCodec.Instance, header), HashOf(header));
        }
    }
}
