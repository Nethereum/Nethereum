using System;
using Nethereum.Model;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class BlockHeaderUnsigned64FieldTests
    {
        [Theory]
        [InlineData("frontier")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public void Given_AHeaderWhoseUint64FieldsAreAbove2Pow63_When_RoundTripped_Then_TheBitsSurvive(string shape)
        {
            var header = BlockHeaderShapes.For(shape);
            header.GasLimit = unchecked((long)0x8000000000000000UL);
            header.GasUsed = -1;
            header.Timestamp = unchecked((long)0xFFFFFFFFFFFFFFF0UL);

            var decoded = BlockHeaderEncoder.Current.Decode(BlockHeaderEncoder.Current.Encode(header));

            Assert.Equal(header.GasLimit, decoded.GasLimit);
            Assert.Equal(header.GasUsed, decoded.GasUsed);
            Assert.Equal(header.Timestamp, decoded.Timestamp);
            Assert.Equal(
                BlockHeaderEncoder.Current.Encode(header),
                BlockHeaderEncoder.Current.Encode(decoded));
        }

        [Fact]
        public void Given_AGasLimitWiderThan64Bits_When_Decoded_Then_TheDecoderRefusesIt()
        {
            var header = BlockHeaderShapes.For("london");
            var wire = BlockHeaderEncoder.Current.Encode(header);
            var collection = (RLP.RLPCollection)RLP.RLP.Decode(wire);
            var fields = new byte[collection.Count][];
            for (var i = 0; i < fields.Length; i++)
                fields[i] = RLP.RLP.EncodeElement(collection[i].RLPData);
            fields[9] = RLP.RLP.EncodeElement(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 });

            Assert.ThrowsAny<Exception>(() => BlockHeaderEncoder.Current.Decode(RLP.RLP.EncodeList(fields)));
        }
    }
}
