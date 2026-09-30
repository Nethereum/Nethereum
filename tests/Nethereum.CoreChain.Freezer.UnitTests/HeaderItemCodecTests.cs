using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class HeaderItemCodecTests
    {
        private readonly HeaderItemCodec _codec = new();

        [Fact]
        public void Given_RealGethHeadersItem_When_Decode_Then_BlockNumberMatchesCorpusOrigin()
        {
            var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "headers", compressed: true, itemNumber: 0);

            var header = _codec.Decode(raw);

            Assert.Equal(1_500_000L, header.BlockNumber.ToLong());
        }

        [Fact]
        public void Given_RealGethHeaderItem_When_Hashed_Then_MatchesCorpusHashesTableEntry()
        {
            var rawHeader = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "headers", compressed: true, itemNumber: 0);
            var rawHash = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "hashes", compressed: false, itemNumber: 0);

            var computedHash = new Sha3Keccack().CalculateHash(rawHeader);

            Assert.Equal(rawHash, computedHash);
        }

        [Fact]
        public void Given_RealGethHeaderItems_When_DecodeThenReEncode_Then_ByteIdenticalToGeth()
        {
            var count = CorpusFixture.ItemCount(CorpusFixture.TxsSliceDirectory, "headers", compressed: true);

            for (var item = 0L; item < count; item++)
            {
                var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "headers", compressed: true, item);

                var header = _codec.Decode(raw);
                var reencoded = _codec.Encode(header);

                Assert.Equal(raw, reencoded);
            }
        }

        [Fact]
        public void Given_RealGethGenesisRegionHeaderItems_When_DecodeThenReEncode_Then_ByteIdenticalToGeth()
        {
            var count = CorpusFixture.ItemCount(CorpusFixture.GenesisSliceDirectory, "headers", compressed: true);

            for (var item = 0L; item < count; item++)
            {
                var raw = CorpusFixture.ReadRawItem(CorpusFixture.GenesisSliceDirectory, "headers", compressed: true, item);

                var header = _codec.Decode(raw);
                var reencoded = _codec.Encode(header);

                Assert.Equal(raw, reencoded);
            }
        }
    }
}
