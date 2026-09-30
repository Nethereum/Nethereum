using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.CoreChain.Models;
using Nethereum.Documentation;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests.FilterMaps
{
    public class FilterMapsQueryEngineTests
    {
        private static readonly FilterMapsParams P = FilterMapsTestSupport.TestParams;

        private static FilterMapsQueryEngine BuildEngine(IFilterMapsStore store, IChainView chain, IHistoricalLogScan hotScan) =>
            new FilterMapsQueryEngine(
                store,
                new FilterMapsMatcher(new FilterMapsQueryBackend(store, P)),
                new FilterMapsLogResolver(store, chain, P),
                hotScan);


        [Fact]
        public async Task Given_CorpusRenderedIndex_When_QueryByAddress_Then_ReturnsExactlyTheAddressLogs()
        {
            var chain = new CorpusChainView();
            var store = new InMemoryFilterMapsStore();
            var finality = new FakeFinalitySource { FinalizedBlockNumber = chain.HeadNumber };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);

            for (var i = 0; i < 40 && indexer.RenderHead(); i++) { }
            var indexedHead = indexer.IndexedHeadBlock;
            Assert.True(indexedHead > 0, "expected at least one rendered block from the corpus");

            var truth = IndependentlyScanCorpus(chain, indexedHead);
            Assert.NotEmpty(truth);

            var targetAddress = truth[0].log.Address;
            var expected = truth.Where(t => t.log.Address.IsTheSameAddress(targetAddress)).ToList();
            Assert.NotEmpty(expected);

            var engine = BuildEngine(store, chain, new FakeHistoricalLogScan());
            var filter = new LogFilter { Addresses = new List<string> { targetAddress }, FromBlock = 0, ToBlock = indexedHead };
            var result = await engine.GetLogsAsync(filter);

            AssertExactMatch(expected, result);

            var withTopic = truth.FirstOrDefault(t => t.log.Topics.Count > 0);
            Assert.True(withTopic.log != null, "expected at least one corpus log with a topic in the rendered range");
            {
                var expectedWithTopic = truth
                    .Where(t => t.log.Address.IsTheSameAddress(withTopic.log.Address) &&
                                t.log.Topics.Count > 0 &&
                                BytesEqual(t.log.Topics[0], withTopic.log.Topics[0]))
                    .ToList();
                Assert.NotEmpty(expectedWithTopic);

                var topicFilter = new LogFilter
                {
                    Addresses = new List<string> { withTopic.log.Address },
                    Topics = new List<List<byte[]>> { new List<byte[]> { withTopic.log.Topics[0] } },
                    FromBlock = 0,
                    ToBlock = indexedHead,
                };
                var topicResult = await engine.GetLogsAsync(topicFilter);
                AssertExactMatch(expectedWithTopic, topicResult);
            }
        }


        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Query logs across the indexed/hot boundary")]
        public async Task Given_QuerySpanningIndexedHead_When_GetLogs_Then_SplitAtSnapshottedBoundary_NoGapNoDouble()
        {
            var chain = new FakeChainView();
            chain.SetBlock(0,
                FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 1, 2, 3 }),
                FilterMapsTestSupport.MakeLog(addressSeed: 2, topicSeeds: new byte[] { 4, 5, 6 }));
            chain.SetBlock(1,
                FilterMapsTestSupport.MakeLog(addressSeed: 3, topicSeeds: new byte[] { 7, 8, 9, 10, 11, 12 }));

            var store = new InMemoryFilterMapsStore();
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 1 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);
            Assert.True(indexer.RenderHead());
            Assert.Equal(1, indexer.IndexedHeadBlock);

            var scanLog = new ResolvedLog(3, 0, 0, FilterMapsTestSupport.MakeLog(addressSeed: 9));
            var hotScan = new FakeHistoricalLogScan { Result = new List<ResolvedLog> { scanLog } };

            var engine = BuildEngine(store, chain, hotScan);
            var filter = new LogFilter { FromBlock = 0, ToBlock = 3 };
            var result = await engine.GetLogsAsync(filter);

            Assert.True(hotScan.RequestedRange.HasValue);
            Assert.Equal(2L, hotScan.RequestedRange.Value.from);
            Assert.Equal(3L, hotScan.RequestedRange.Value.to);

            Assert.Equal(4, result.Count);
            Assert.Equal(2, result.Count(r => r.BlockNumber == 0));
            Assert.Equal(1, result.Count(r => r.BlockNumber == 1));
            Assert.Equal(1, result.Count(r => r.BlockNumber == 3));
            Assert.True(result.Select(r => r.BlockNumber).SequenceEqual(result.Select(r => r.BlockNumber).OrderBy(b => b)));
        }


        [Fact]
        public void Given_FalsePositiveLvIndex_When_Resolve_Then_Dropped()
        {
            var chain = new FakeChainView();
            chain.SetBlock(0, FilterMapsTestSupport.MakeLog(addressSeed: 1, topicSeeds: new byte[] { 5, 6 }));

            var store = new InMemoryFilterMapsStore();
            store.WriteBlockLvPointer(0, 0);
            store.WriteBlockLvPointer(1, 3);
            store.WriteRange(new FilterMapsRange(2, headIndexed: true, headDelimiter: 3,
                blocksFirst: 0, blocksAfterLast: 1, mapsFirst: 0, mapsAfterLast: 1, tailPartialEpoch: 0));

            var resolver = new FilterMapsLogResolver(store, chain, P);

            var addressHit = resolver.GetLogByLvIndex(0);
            Assert.NotNull(addressHit);
            Assert.Equal(0, addressHit.LogIndex);

            Assert.Null(resolver.GetLogByLvIndex(1));
            Assert.Null(resolver.GetLogByLvIndex(2));
        }


        [Fact]
        public async Task Given_RowCollidingAddresses_When_QueryOneAddress_Then_OtherAddressLogExcluded()
        {
            var seeds = FilterMapsTestSupport.FindColumnCollidingAddressSeeds(mapIndex: 0, count: 2, P);
            var seedA = seeds[0];
            var seedB = seeds[1];

            var chain = new FakeChainView();
            var logA = new Log { Address = FilterMapsTestSupport.Address(seedA), Data = null, Topics = new List<byte[]>() };
            var logB = new Log { Address = FilterMapsTestSupport.Address(seedB), Data = null, Topics = new List<byte[]>() };
            var fillerMap0 = FilterMapsTestSupport.MakeLog(addressSeed: 250, topicSeeds: new byte[] { 1, 2, 3, 4, 5 });
            chain.SetBlock(0, logA, logB, fillerMap0);
            var fillerMap1 = FilterMapsTestSupport.MakeLog(addressSeed: 251, topicSeeds: new byte[] { 6, 7, 8, 9, 10, 11 });
            chain.SetBlock(1, fillerMap1);

            var store = new InMemoryFilterMapsStore();
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 1 };
            var indexer = new FilterMapsIndexer(store, chain, finality, P);
            Assert.True(indexer.RenderHead());
            Assert.Equal(1, indexer.IndexedHeadBlock);

            var engine = BuildEngine(store, chain, new FakeHistoricalLogScan());
            var filter = new LogFilter
            {
                Addresses = new List<string> { FilterMapsTestSupport.Address(seedA) },
                FromBlock = 0,
                ToBlock = 1,
            };
            var result = await engine.GetLogsAsync(filter);

            Assert.Single(result);
            Assert.Equal(0, result[0].BlockNumber);
            Assert.True(result[0].Log.Address.IsTheSameAddress(FilterMapsTestSupport.Address(seedA)));
            Assert.False(result[0].Log.Address.IsTheSameAddress(FilterMapsTestSupport.Address(seedB)));
        }


        private static List<(long block, int tx, int logIndex, Log log)> IndependentlyScanCorpus(IChainView chain, long indexedHead)
        {
            var found = new List<(long, int, int, Log)>();
            for (var b = 0L; b <= indexedHead; b++)
            {
                var receipts = chain.Receipts(b);
                var logIndex = 0;
                for (var tx = 0; tx < receipts.Count; tx++)
                {
                    var logs = receipts[tx].Logs;
                    for (var l = 0; l < logs.Count; l++)
                    {
                        found.Add((b, tx, logIndex, logs[l]));
                        logIndex++;
                    }
                }
            }
            return found;
        }

        private static void AssertExactMatch(IReadOnlyList<(long block, int tx, int logIndex, Log log)> expected, IReadOnlyList<ResolvedLog> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].block, actual[i].BlockNumber);
                Assert.Equal(expected[i].tx, actual[i].TransactionIndex);
                Assert.Equal(expected[i].logIndex, actual[i].LogIndex);
                Assert.True(expected[i].log.Address.IsTheSameAddress(actual[i].Log.Address));
                Assert.True(BytesEqual(expected[i].log.Data, actual[i].Log.Data));
                Assert.Equal(expected[i].log.Topics.Count, actual[i].Log.Topics.Count);
                for (var t = 0; t < expected[i].log.Topics.Count; t++)
                    Assert.True(BytesEqual(expected[i].log.Topics[t], actual[i].Log.Topics[t]));
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || a.Length == 0) return b == null || b.Length == 0;
            if (b == null) return false;
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private sealed class FakeHistoricalLogScan : IHistoricalLogScan
        {
            public List<ResolvedLog> Result { get; set; } = new List<ResolvedLog>();
            public (long from, long to)? RequestedRange { get; private set; }

            public Task<IReadOnlyList<ResolvedLog>> ScanAsync(LogFilter filter, long fromBlock, long toBlock)
            {
                RequestedRange = (fromBlock, toBlock);
                return Task.FromResult<IReadOnlyList<ResolvedLog>>(Result);
            }
        }
    }
}
