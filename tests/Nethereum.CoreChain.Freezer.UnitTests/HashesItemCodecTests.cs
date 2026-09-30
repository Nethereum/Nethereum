using Nethereum.CoreChain.Freezer.Codecs;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class HashesItemCodecTests
    {
        private readonly HashesItemCodec _codec = new();

        [Fact]
        public void Given_RealGethHashesItem_When_Decode_Then_Is32Bytes()
        {
            var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "hashes", compressed: false, itemNumber: 0);

            var hash = _codec.Decode(raw);

            Assert.Equal(32, hash.Length);
        }

        [Fact]
        public void Given_RealGethHashesItems_When_DecodeThenReEncode_Then_ByteIdenticalToGeth()
        {
            var count = CorpusFixture.ItemCount(CorpusFixture.TxsSliceDirectory, "hashes", compressed: false);

            for (var item = 0L; item < count; item++)
            {
                var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "hashes", compressed: false, item);

                var hash = _codec.Decode(raw);
                var reencoded = _codec.Encode(hash);

                Assert.Equal(raw, reencoded);
            }
        }
    }
}
