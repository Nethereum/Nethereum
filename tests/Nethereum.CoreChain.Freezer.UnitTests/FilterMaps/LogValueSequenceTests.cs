using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests.FilterMaps
{
    public class LogValueSequenceTests
    {
        [Fact]
        public void Given_BlockWithLogs_When_LogValueSequence_Then_AddressThenTopicsInOrderWithDelimiter()
        {
            var chain = new FakeChainView();
            var log0 = FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 10, 11 });
            var log1 = FilterMapsTestSupport.MakeLog(addressSeed: 2, topicSeeds: new byte[] { 12 });
            chain.SetBlock(0, log0, log1);
            chain.SetBlock(1, FilterMapsTestSupport.MakeLog(addressSeed: 3, topicSeeds: new byte[] { 13 }));

            var entries = LogValueSequence.Enumerate(0, 1, startLvIndex: 0, precedingBlock: null, chain, FilterMapsTestSupport.TestParams).ToList();

            Assert.Equal(8, entries.Count);

            AssertValue(entries[0], 0, 0, false);
            AssertValue(entries[1], 0, 1, true);
            AssertValue(entries[2], 0, 2, true);
            AssertValue(entries[3], 0, 3, false);
            AssertValue(entries[4], 0, 4, true);

            Assert.True(entries[5].IsBlockDelimiter);
            Assert.Equal(0, entries[5].BlockNumber);
            Assert.Equal(5, entries[5].LvIndex);
            Assert.Null(entries[5].ValueHash);

            AssertValue(entries[6], 1, 6, false);
            AssertValue(entries[7], 1, 7, true);

            static void AssertValue(LogValueEntry e, long block, long lvIndex, bool isTopic)
            {
                Assert.False(e.IsBlockDelimiter);
                Assert.Equal(block, e.BlockNumber);
                Assert.Equal(lvIndex, e.LvIndex);
                Assert.NotNull(e.ValueHash);
                Assert.Equal(32, e.ValueHash.Length);
            }
        }

        [Fact]
        public void Given_LogGroupNearMapBoundary_When_Enumerate_Then_SkipsToBoundaryNotSplit()
        {
            var chain = new FakeChainView();
            var log0 = FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 1, 2, 3, 4, 5 });
            var log1 = FilterMapsTestSupport.MakeLog(addressSeed: 2, topicSeeds: new byte[] { 6, 7, 8 });
            chain.SetBlock(0, log0, log1);

            var entries = LogValueSequence.Enumerate(0, 0, startLvIndex: 0, precedingBlock: null, chain, FilterMapsTestSupport.TestParams).ToList();

            Assert.Equal(10, entries.Count);
            Assert.Equal(0, entries[0].LvIndex);
            Assert.Equal(5, entries[5].LvIndex);
            Assert.Equal(8, entries[6].LvIndex);
            Assert.Equal(9, entries[7].LvIndex);
            Assert.Equal(10, entries[8].LvIndex);
            Assert.Equal(11, entries[9].LvIndex);

            var p = FilterMapsTestSupport.TestParams;
            Assert.Equal(0, entries[5].LvIndex >> p.LogValuesPerMap);
            Assert.Equal(1, entries[6].LvIndex >> p.LogValuesPerMap);
        }

        [Fact]
        public void Given_TwoBlocks_When_Enumerate_Then_SecondBlockAddressLvIndexAccountsForFirstBlockPlusDelimiter()
        {
            var chain = new FakeChainView();
            chain.SetBlock(0, FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 1, 2, 3 }));
            chain.SetBlock(1, FilterMapsTestSupport.MakeLog(addressSeed: 2, topicSeeds: new byte[] { 4 }));

            var entries = LogValueSequence.Enumerate(0, 1, startLvIndex: 0, precedingBlock: null, chain, FilterMapsTestSupport.TestParams).ToList();

            var block0GroupSize = 4;
            var delimiterSlots = 1;
            var expectedBlock1AddressLvIndex = block0GroupSize + delimiterSlots;

            var block1Address = entries.First(e => !e.IsBlockDelimiter && e.BlockNumber == 1);
            Assert.Equal(expectedBlock1AddressLvIndex, block1Address.LvIndex);

            var delimiter = entries.Single(e => e.IsBlockDelimiter);
            Assert.Equal(block0GroupSize, delimiter.LvIndex);
            Assert.Equal(delimiter.LvIndex + 1, block1Address.LvIndex);
        }
    }
}
