using System;
using Nethereum.Model;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class BlockHeaderFieldWidthTests
    {
        private static byte[] WithFieldReplaced(string shape, int index, byte[] replacement)
        {
            var wire = BlockHeaderEncoder.Current.Encode(BlockHeaderShapes.For(shape));
            var collection = (RLP.RLPCollection)RLP.RLP.Decode(wire);
            var fields = new byte[collection.Count][];
            for (var i = 0; i < fields.Length; i++)
                fields[i] = RLP.RLP.EncodeElement(i == index ? replacement : collection[i].RLPData);
            return RLP.RLP.EncodeList(fields);
        }

        [Theory]
        [InlineData("frontier", 0, 31)]
        [InlineData("frontier", 1, 33)]
        [InlineData("frontier", 2, 19)]
        [InlineData("frontier", 3, 31)]
        [InlineData("frontier", 4, 31)]
        [InlineData("frontier", 5, 31)]
        [InlineData("frontier", 6, 255)]
        [InlineData("frontier", 13, 31)]
        [InlineData("frontier", 14, 7)]
        [InlineData("shanghai", 16, 31)]
        [InlineData("cancun", 19, 31)]
        [InlineData("prague", 20, 31)]
        [InlineData("amsterdam", 21, 31)]
        public void Given_AHashOrBloomFieldOfTheWrongWidth_When_Decoded_Then_TheDecoderRefusesTheHeader(
            string shape, int index, int width)
        {
            var wire = WithFieldReplaced(shape, index, new byte[width]);

            Assert.ThrowsAny<Exception>(() => BlockHeaderEncoder.Current.Decode(wire));
        }

        [Theory]
        [InlineData("frontier")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public void Given_AHeaderWithEveryFieldAtItsWidth_When_Decoded_Then_TheDecoderAcceptsIt(string shape)
        {
            var wire = BlockHeaderEncoder.Current.Encode(BlockHeaderShapes.For(shape));

            var decoded = BlockHeaderEncoder.Current.Decode(wire);

            Assert.Equal(32, decoded.MixHash.Length);
            Assert.Equal(8, decoded.Nonce.Length);
        }
    }
}
