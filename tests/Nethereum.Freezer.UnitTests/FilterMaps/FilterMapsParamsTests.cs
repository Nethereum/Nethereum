using System;
using Nethereum.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.Freezer.UnitTests.FilterMaps
{
    public class FilterMapsParamsTests
    {
        private static FilterMapsParams With(int logMapWidth = 24, int baseRowGroupSize = 32) =>
            new FilterMapsParams(
                logMapHeight: 16, logMapWidth: logMapWidth, logMapsPerEpoch: 10, logValuesPerMap: 16,
                baseRowGroupSize: baseRowGroupSize, baseRowLengthRatio: 8, logLayerDiff: 4);

        [Fact]
        public void Given_DefaultParams_When_Sanitize_Then_DoesNotThrow()
        {
            FilterMapsParams.Default.Sanitize();
        }

        [Fact]
        public void Given_LogMapWidthNotMultipleOf8_When_Sanitize_Then_Throws()
        {
            Assert.Throws<ArgumentException>(() => With(logMapWidth: 25).Sanitize());
        }

        [Fact]
        public void Given_BaseRowGroupSizeNotPowerOf2_When_Sanitize_Then_Throws()
        {
            Assert.Throws<ArgumentException>(() => With(baseRowGroupSize: 30).Sanitize());
        }

        [Fact]
        public void Given_MapsPerEpochNotMultipleOfBaseRowGroupSize_When_Sanitize_Then_Throws()
        {
            var p = new FilterMapsParams(
                logMapHeight: 16, logMapWidth: 24, logMapsPerEpoch: 2, logValuesPerMap: 16,
                baseRowGroupSize: 32, baseRowLengthRatio: 8, logLayerDiff: 4);
            Assert.Throws<ArgumentException>(() => p.Sanitize());
        }

        [Theory]
        [InlineData(0, 8)]
        [InlineData(1, 128)]
        [InlineData(2, 2048)]
        [InlineData(3, 8192)]
        [InlineData(4, 8192)]
        public void Given_DefaultParams_When_MaxRowLength_Then_Yields_8_128_2048_8192_8192(int layer, int expected)
        {
            var p = FilterMapsParams.Default;

            Assert.Equal(expected, p.MaxRowLength(layer));
        }

        [Fact]
        public void Given_Default_When_BaseRowLength_Then_8()
        {
            var p = FilterMapsParams.Default;

            Assert.Equal(8, p.BaseRowLength);
        }

        [Fact]
        public void Given_Default_When_MapHeight_Then_65536()
        {
            var p = FilterMapsParams.Default;

            Assert.Equal(65536, p.MapHeight);
        }

        [Fact]
        public void Given_LayerAboveSaturation_When_MaxRowLength_Then_StaysAt8192()
        {
            var p = FilterMapsParams.Default;

            Assert.Equal(8192, p.MaxRowLength(3));
            Assert.Equal(p.MaxRowLength(4), p.MaxRowLength(100));
        }
    }
}
