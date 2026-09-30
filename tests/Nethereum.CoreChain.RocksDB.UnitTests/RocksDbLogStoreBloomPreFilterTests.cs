using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbLogStoreBloomPreFilterTests : IDisposable
    {
        private readonly RocksDbTestFixture _fixture;

        public RocksDbLogStoreBloomPreFilterTests()
        {
            _fixture = new RocksDbTestFixture();
        }

        public void Dispose() => _fixture.Dispose();

        private static readonly string AddressA = "0x" + new string('a', 40);
        private static readonly string AddressB = "0x" + new string('b', 40);

        private static byte[] Topic(byte fill) => Enumerable.Repeat(fill, 32).ToArray();

        private static byte[] MakeHash(int seed)
        {
            var hash = new byte[32];
            hash[0] = (byte)(seed & 0xFF);
            hash[1] = (byte)((seed >> 8) & 0xFF);
            return hash;
        }

        private async Task SaveBlockAsync(BigInteger blockNumber, List<Log> logs)
        {
            var blockHash = MakeHash((int)blockNumber);
            var txHash = MakeHash((int)blockNumber + 100_000);
            await _fixture.LogStore.SaveLogsAsync(logs, txHash, blockHash, blockNumber, 0);

            var bloom = new LogBloomFilter();
            foreach (var log in logs) bloom.AddLog(log);
            await _fixture.LogStore.SaveBlockBloomAsync(blockNumber, bloom.Data);
        }

        [Fact]
        public async Task GetLogs_TwoAddressFilter_BlockHasOnlyFirstAddress_ReturnsIt()
        {
            var log = new Log { Address = AddressA, Topics = new List<byte[]> { Topic(0x11) }, Data = new byte[] { 0x01 } };
            await SaveBlockAsync(10, new List<Log> { log });

            var filter = new LogFilter { FromBlock = 10, ToBlock = 10, Addresses = new List<string> { AddressA, AddressB } };
            var result = await _fixture.LogStore.GetLogsAsync(filter);

            Assert.Single(result);
            Assert.Equal(AddressA.ToLowerInvariant(), result[0].Address.ToLowerInvariant());
        }

        [Fact]
        public async Task GetLogs_TopicPosition_BlockHasOnlyOneAlternative_ReturnsIt()
        {
            var t1 = Topic(0x11);
            var t2 = Topic(0x22);
            var log = new Log { Address = AddressA, Topics = new List<byte[]> { t1 }, Data = new byte[] { 0x01 } };
            await SaveBlockAsync(20, new List<Log> { log });

            var filter = new LogFilter
            {
                FromBlock = 20,
                ToBlock = 20,
                Topics = new List<List<byte[]>> { new List<byte[]> { t1, t2 } }
            };
            var result = await _fixture.LogStore.GetLogsAsync(filter);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetLogs_AddressAndTopic_BothGroupsMustMatch_ExcludesBlockMissingTopic()
        {
            var required = Topic(0x33);
            var present = Topic(0x44);
            var log = new Log { Address = AddressA, Topics = new List<byte[]> { present }, Data = new byte[] { 0x01 } };
            await SaveBlockAsync(30, new List<Log> { log });

            var filter = new LogFilter
            {
                FromBlock = 30,
                ToBlock = 30,
                Addresses = new List<string> { AddressA, AddressB },
                Topics = new List<List<byte[]>> { new List<byte[]> { required } }
            };
            var result = await _fixture.LogStore.GetLogsAsync(filter);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLogs_MergedVsPerTerm_ResultSuperset()
        {
            var addressC = "0x" + new string('c', 40);
            var t1 = Topic(0x11);
            await SaveBlockAsync(40, new List<Log> { new Log { Address = AddressA, Topics = new List<byte[]> { t1 } } });
            await SaveBlockAsync(41, new List<Log> { new Log { Address = AddressB, Topics = new List<byte[]> { t1 } } });
            await SaveBlockAsync(42, new List<Log> { new Log { Address = addressC, Topics = new List<byte[]> { t1 } } });
            await SaveBlockAsync(43, new List<Log>
            {
                new Log { Address = AddressA, Topics = new List<byte[]> { t1 } },
                new Log { Address = AddressB, Topics = new List<byte[]> { t1 } }
            });
            await SaveBlockAsync(44, new List<Log> { new Log { Address = addressC, Topics = new List<byte[]> { Topic(0x99) } } });

            var filter = new LogFilter { FromBlock = 40, ToBlock = 44, Addresses = new List<string> { AddressA, AddressB } };

            var oldMergedBloom = new LogBloomFilter();
            foreach (var address in filter.Addresses) oldMergedBloom.AddAddress(address);

            var terms = RocksDbLogStore.BuildQueryBloomTerms(filter);
            Assert.NotNull(terms);

            var sawFalseNegativeFixed = false;
            for (BigInteger b = 40; b <= 44; b++)
            {
                var blockBloom = new LogBloomFilter();
                foreach (var log in await _fixture.LogStore.GetLogsByBlockNumberAsync(b)) blockBloom.AddLog(log.ToLog());

                var oldMatches = oldMergedBloom.Matches(blockBloom.Data);
                var newMatches = RocksDbLogStore.MatchesQueryBloomTerms(terms, blockBloom.Data);

                if (oldMatches)
                {
                    Assert.True(newMatches, $"block {b}: new candidate set must be a superset of the old one");
                }
                if (b == 40 && newMatches && !oldMatches)
                {
                    sawFalseNegativeFixed = true;
                }
            }
            Assert.True(sawFalseNegativeFixed, "expected block 40 (A-only) to be a candidate under the fix but not under the old merged-bloom check");

            var expected = new List<FilteredLog>();
            for (BigInteger b = 40; b <= 44; b++)
            {
                foreach (var log in await _fixture.LogStore.GetLogsByBlockNumberAsync(b))
                {
                    if (filter.MatchesAddress(log.Address) && filter.MatchesTopics(log.Topics))
                        expected.Add(log);
                }
            }

            var actual = await _fixture.LogStore.GetLogsAsync(filter);

            Assert.Equal(4, expected.Count);
            Assert.Equal(expected.Count, actual.Count);
        }
    }
}
