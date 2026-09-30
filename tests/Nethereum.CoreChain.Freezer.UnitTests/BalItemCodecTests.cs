using Nethereum.CoreChain.Freezer.Codecs;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class BalItemCodecTests
    {
        private readonly BalItemCodec _codec = new();

        [Fact]
        public void Given_EmptyBalBytes_When_RoundTrip_Then_Identity()
        {
            var raw = CorpusFixture.ReadRawItem(CorpusFixture.GenesisSliceDirectory, "bals", compressed: true, itemNumber: 0);
            Assert.Empty(raw);

            var decoded = _codec.Decode(raw);
            Assert.Empty(decoded);

            var reencoded = _codec.Encode(decoded);
            Assert.Equal(raw, reencoded);
        }

        [Fact]
        public void Given_RealGethGenesisBalsItems_When_DecodeThenReEncode_Then_ByteIdenticalToGeth()
        {
            var count = CorpusFixture.ItemCount(CorpusFixture.GenesisSliceDirectory, "bals", compressed: true);

            for (var item = 0L; item < count; item++)
            {
                var raw = CorpusFixture.ReadRawItem(CorpusFixture.GenesisSliceDirectory, "bals", compressed: true, item);

                var decoded = _codec.Decode(raw);
                var reencoded = _codec.Encode(decoded);

                Assert.Equal(raw, reencoded);
            }
        }
    }
}
