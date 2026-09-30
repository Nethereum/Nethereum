using System.Text;
using Nethereum.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.Freezer.UnitTests.FilterMaps
{
    public class FilterMapsSchemaTests
    {
        private static readonly FilterMapsParams Params = FilterMapsParams.Default;

        [Fact]
        public void Given_RangeKey_Then_IsFmDashRBytes()
        {
            var key = FilterMapsSchema.RangeKey();

            Assert.Equal(Encoding.ASCII.GetBytes("fm-R"), key);
        }

        [Fact]
        public void Given_BaseRowKey_Then_Is9BytesWith0x00Suffix()
        {
            var key = FilterMapsSchema.BaseRowKey(mapRowIndex: 0x0102030405060708);

            Assert.Equal(13, key.Length);
            Assert.Equal(Encoding.ASCII.GetBytes("fm-r"), key[..4]);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, key[4..12]);
            Assert.Equal((byte)0x00, key[12]);
        }

        [Fact]
        public void Given_ExtRowKey_Then_IsPlain8Bytes()
        {
            var key = FilterMapsSchema.ExtRowKey(mapRowIndex: 0x0102030405060708);

            Assert.Equal(12, key.Length);
            Assert.Equal(Encoding.ASCII.GetBytes("fm-r"), key[..4]);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, key[4..]);
        }

        [Fact]
        public void Given_BaseVsExtKey_When_Compared_Then_DifferByExactly_The0x00Suffix()
        {
            const long mapRowIndex = 424242;

            var baseKey = FilterMapsSchema.BaseRowKey(mapRowIndex);
            var extKey = FilterMapsSchema.ExtRowKey(mapRowIndex);

            Assert.Equal(extKey.Length + 1, baseKey.Length);
            for (var i = 0; i < extKey.Length; i++)
            {
                Assert.Equal(extKey[i], baseKey[i]);
            }
            Assert.Equal((byte)0x00, baseKey[baseKey.Length - 1]);
        }

        [Fact]
        public void Given_LastBlockOfMapKey_Then_IsFmDashBPlusU32BE()
        {
            var key = FilterMapsSchema.LastBlockOfMapKey(mapIndex: 0x01020304);

            Assert.Equal(8, key.Length);
            Assert.Equal(Encoding.ASCII.GetBytes("fm-b"), key[..4]);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, key[4..]);
        }

        [Fact]
        public void Given_BlockLvPointerKey_Then_IsFmDashPPlusU64BE()
        {
            var key = FilterMapsSchema.BlockLvPointerKey(blockNumber: 0x0102030405060708);

            Assert.Equal(12, key.Length);
            Assert.Equal(Encoding.ASCII.GetBytes("fm-p"), key[..4]);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, key[4..]);
        }

        [Fact]
        public void Given_KnownMapIndexAndRowIndex_When_MapRowIndex_Then_MatchesHandComputedValue()
        {
            var result = FilterMapsSchema.MapRowIndex(mapIndex: 2055, rowIndex: 100, Params);

            Assert.Equal(134320135L, result);
        }

        [Fact]
        public void Given_MapIndexInEpochZero_When_MapRowIndex_Then_OnlyRowAndSubIndexContribute()
        {
            var result = FilterMapsSchema.MapRowIndex(mapIndex: 5, rowIndex: 3, Params);

            Assert.Equal(3077L, result);
        }
    }
}
