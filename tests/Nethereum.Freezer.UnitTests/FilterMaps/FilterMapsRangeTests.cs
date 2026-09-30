using Nethereum.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.Freezer.UnitTests.FilterMaps
{
    public class FilterMapsRangeTests
    {
        [Fact]
        public void Given_ARange_When_EncodeDecode_Then_RoundTripsIdentically()
        {
            var range = new FilterMapsRange(
                version: 2,
                headIndexed: true,
                headDelimiter: 123456,
                blocksFirst: 100,
                blocksAfterLast: 5000,
                mapsFirst: 0,
                mapsAfterLast: 42,
                tailPartialEpoch: 3);

            var decoded = FilterMapsRange.Decode(range.Encode());

            Assert.Equal(range.Version, decoded.Version);
            Assert.Equal(range.HeadIndexed, decoded.HeadIndexed);
            Assert.Equal(range.HeadDelimiter, decoded.HeadDelimiter);
            Assert.Equal(range.BlocksFirst, decoded.BlocksFirst);
            Assert.Equal(range.BlocksAfterLast, decoded.BlocksAfterLast);
            Assert.Equal(range.MapsFirst, decoded.MapsFirst);
            Assert.Equal(range.MapsAfterLast, decoded.MapsAfterLast);
            Assert.Equal(range.TailPartialEpoch, decoded.TailPartialEpoch);
        }

        [Fact]
        public void Given_HeadIndexedFalse_When_EncodeDecode_Then_RoundTripsAsFalse()
        {
            var range = new FilterMapsRange(1, false, 0, 0, 0, 0, 0, 0);

            var decoded = FilterMapsRange.Decode(range.Encode());

            Assert.False(decoded.HeadIndexed);
        }

        [Fact]
        public void Given_HeadIndexedTrue_When_EncodeDecode_Then_RoundTripsAsTrue()
        {
            var range = new FilterMapsRange(1, true, 0, 0, 0, 0, 0, 0);

            var decoded = FilterMapsRange.Decode(range.Encode());

            Assert.True(decoded.HeadIndexed);
        }

        [Fact]
        public void Given_AllZeroRange_When_EncodeDecode_Then_RoundTripsIdentically()
        {
            var range = new FilterMapsRange(0, false, 0, 0, 0, 0, 0, 0);

            var decoded = FilterMapsRange.Decode(range.Encode());

            Assert.Equal(0u, decoded.Version);
            Assert.False(decoded.HeadIndexed);
            Assert.Equal(0, decoded.HeadDelimiter);
            Assert.Equal(0, decoded.BlocksFirst);
            Assert.Equal(0, decoded.BlocksAfterLast);
            Assert.Equal(0, decoded.MapsFirst);
            Assert.Equal(0, decoded.MapsAfterLast);
            Assert.Equal(0, decoded.TailPartialEpoch);
        }
    }
}
