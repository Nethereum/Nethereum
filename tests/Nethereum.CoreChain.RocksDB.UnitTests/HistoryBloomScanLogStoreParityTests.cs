using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HistoryBloomScanLogStoreParityTests : IDisposable
    {
        private readonly RocksDbTestFixture _fixture;
        private readonly HistoryBloomScanLogStore _l1;

        private static readonly string AddrA = "0x" + new string('a', 40);
        private static readonly string AddrB = "0x" + new string('b', 40);
        private static readonly string AddrC = "0x" + new string('c', 40);
        private static readonly string AddrD = "0x" + new string('d', 40);
        private static readonly string AddrZ = "0x" + new string('e', 40);

        private static readonly byte[] T0 = Topic(0x10);
        private static readonly byte[] T1 = Topic(0x11);
        private static readonly byte[] T2 = Topic(0x12);
        private static readonly byte[] T3 = Topic(0x13);
        private static readonly byte[] T9 = Topic(0x19);

        public HistoryBloomScanLogStoreParityTests()
        {
            _fixture = new RocksDbTestFixture();
            _l1 = new HistoryBloomScanLogStore(_fixture.Manager);
            SeedCorpusAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _fixture.Dispose();

        private static byte[] Topic(byte fill) => Enumerable.Repeat(fill, 32).ToArray();

        private static byte[] Hash(int seed)
        {
            var h = new byte[32];
            h[0] = (byte)seed;
            h[1] = (byte)(seed >> 8);
            h[2] = (byte)(seed >> 16);
            return h;
        }

        private static byte[] BlockHashOf(int blockNumber) => Hash(blockNumber * 1000);
        private static byte[] TxHashOf(int blockNumber, int txIndex) => Hash(blockNumber * 1000 + txIndex + 1);

        private static List<Log> OneLog(string address, params byte[][] topics)
            => new List<Log> { new Log { Address = address, Topics = topics.ToList(), Data = new byte[] { 0x01 } } };

        private static BlockHeader MakeHeader(BigInteger blockNumber, byte[] bloom) => new BlockHeader
        {
            ParentHash = Topic(0x00),
            UnclesHash = Topic(0x00),
            Coinbase = "0x" + new string('0', 40),
            StateRoot = Topic(0x00),
            TransactionsHash = Topic(0x00),
            ReceiptHash = Topic(0x00),
            LogsBloom = bloom,
            Difficulty = 0,
            BlockNumber = blockNumber,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 0,
            ExtraData = Array.Empty<byte>(),
            MixHash = Topic(0x00),
            Nonce = new byte[8],
        };

        private async Task SaveBlockAsync(int blockNumber, List<List<Log>> txLogs, byte[] extraBloomBits = null)
        {
            var blockHash = BlockHashOf(blockNumber);
            var bloom = new LogBloomFilter();

            for (int txIndex = 0; txIndex < txLogs.Count; txIndex++)
            {
                var logs = txLogs[txIndex];
                foreach (var log in logs) bloom.AddLog(log);

                var txHash = TxHashOf(blockNumber, txIndex);
                var receipt = Receipt.CreateStatusReceipt(true, 0, null, logs);
                await _fixture.ReceiptStore.SaveAsync(receipt, txHash, blockHash, blockNumber, txIndex, 0, null, 0);

                if (logs.Count > 0)
                    await _fixture.LogStore.SaveLogsAsync(logs, txHash, blockHash, blockNumber, txIndex);
            }

            if (extraBloomBits != null)
                for (int i = 0; i < 256; i++) bloom.Data[i] |= extraBloomBits[i];

            await _fixture.BlockStore.SaveAsync(MakeHeader(blockNumber, bloom.Data), blockHash);
            await _fixture.LogStore.SaveBlockBloomAsync(blockNumber, bloom.Data);
        }

        private async Task SeedCorpusAsync()
        {
            await SaveBlockAsync(1, new List<List<Log>> { OneLog(AddrA, T0) });
            await SaveBlockAsync(2, new List<List<Log>> { OneLog(AddrB, T1) });
            await SaveBlockAsync(3, new List<List<Log>> { OneLog(AddrA, T0), OneLog(AddrC, T2) });
            await SaveBlockAsync(4, new List<List<Log>> { new List<Log>() });
            await SaveBlockAsync(5, new List<List<Log>> { OneLog(AddrA, T1), OneLog(AddrD, T3) });
            await SaveBlockAsync(6, new List<List<Log>> { OneLog(AddrB, T0, T2) });
            await SaveBlockAsync(7, new List<List<Log>> { OneLog(AddrC, T1, T3) });
            await SaveBlockAsync(8, new List<List<Log>> { OneLog(AddrA, T2) });
            await SaveBlockAsync(9, new List<List<Log>> { OneLog(AddrD, T0) });
            await SaveBlockAsync(10, new List<List<Log>> { OneLog(AddrB, T3) });
            await SaveBlockAsync(11, new List<List<Log>> { OneLog(AddrC, T0), OneLog(AddrA, T0) });
            await SaveBlockAsync(12, new List<List<Log>> { OneLog(AddrD, T1) });

            await SaveBlockAsync(49, new List<List<Log>> { OneLog(AddrA, T0) });
            var addrABits = new LogBloomFilter();
            addrABits.AddAddress(AddrA);
            await SaveBlockAsync(50, new List<List<Log>> { OneLog(AddrZ, T9) }, extraBloomBits: addrABits.Data);
        }

        private static List<FilteredLog> CanonicalSort(List<FilteredLog> logs)
            => logs.OrderBy(l => l.BlockNumber).ThenBy(l => l.TransactionIndex).ThenBy(l => l.LogIndex).ToList();

        private static void AssertLogEqual(FilteredLog expected, FilteredLog actual)
        {
            Assert.True(expected.Address.IsTheSameAddress(actual.Address), $"address: {expected.Address} vs {actual.Address}");
            Assert.Equal(expected.Data ?? Array.Empty<byte>(), actual.Data ?? Array.Empty<byte>());
            Assert.Equal(expected.Topics.Count, actual.Topics.Count);
            for (int i = 0; i < expected.Topics.Count; i++)
                Assert.Equal(expected.Topics[i], actual.Topics[i]);
            Assert.Equal(expected.BlockHash, actual.BlockHash);
            Assert.Equal(expected.BlockNumber, actual.BlockNumber);
            Assert.Equal(expected.TransactionHash, actual.TransactionHash);
            Assert.Equal(expected.TransactionIndex, actual.TransactionIndex);
            Assert.Equal(expected.LogIndex, actual.LogIndex);
            Assert.Equal(expected.Removed, actual.Removed);
        }

        private async Task<List<FilteredLog>> AssertParityAsync(LogFilter filter)
        {
            var l1 = CanonicalSort(await _l1.GetLogsAsync(filter));
            var l2 = CanonicalSort(await _fixture.LogStore.GetLogsAsync(filter));

            Assert.Equal(l2.Count, l1.Count);
            for (int i = 0; i < l1.Count; i++)
                AssertLogEqual(l2[i], l1[i]);

            return l1;
        }

        [Fact]
        public async Task SingleAddress_Parity()
        {
            var filter = new LogFilter { FromBlock = 1, ToBlock = 12, Addresses = new List<string> { AddrA } };
            var result = await AssertParityAsync(filter);
            Assert.Equal(5, result.Count);
        }

        [Fact]
        public async Task MultiAddress_Or_Parity()
        {
            var filter = new LogFilter { FromBlock = 1, ToBlock = 12, Addresses = new List<string> { AddrA, AddrC } };
            var result = await AssertParityAsync(filter);
            Assert.NotEmpty(result);
            Assert.All(result, l => Assert.True(l.Address.IsTheSameAddress(AddrA) || l.Address.IsTheSameAddress(AddrC)));
        }

        [Fact]
        public async Task SingleTopicPosition0_Parity()
        {
            var filter = new LogFilter { FromBlock = 1, ToBlock = 12, Topics = new List<List<byte[]>> { new List<byte[]> { T0 } } };
            var result = await AssertParityAsync(filter);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task MultiTopicPositional_AndAcrossPositions_Parity()
        {
            var filter = new LogFilter
            {
                FromBlock = 1,
                ToBlock = 12,
                Topics = new List<List<byte[]>> { new List<byte[]> { T0 }, new List<byte[]> { T2 } }
            };
            var result = await AssertParityAsync(filter);
            Assert.Single(result);
        }

        [Fact]
        public async Task TopicAlternatives_OrWithinPosition_Parity()
        {
            var filter = new LogFilter
            {
                FromBlock = 1,
                ToBlock = 12,
                Topics = new List<List<byte[]>> { new List<byte[]> { T2, T3 } }
            };
            var result = await AssertParityAsync(filter);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task AddressAndTopic_Combined_Parity()
        {
            var filter = new LogFilter
            {
                FromBlock = 1,
                ToBlock = 12,
                Addresses = new List<string> { AddrA },
                Topics = new List<List<byte[]>> { new List<byte[]> { T0 } }
            };
            var result = await AssertParityAsync(filter);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task NoFilter_WholeRange_Parity()
        {
            var filter = new LogFilter { FromBlock = 1, ToBlock = 12 };
            var result = await AssertParityAsync(filter);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task WideRange_ManyBlocks_Parity()
        {
            var filter = new LogFilter { FromBlock = 1, ToBlock = 50, Addresses = new List<string> { AddrA, AddrD } };
            var result = await AssertParityAsync(filter);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task FromBlockEqualsToBlock_SingleBlock_Parity()
        {
            var filter = new LogFilter { FromBlock = 11, ToBlock = 11 };
            var result = await AssertParityAsync(filter);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task EmptyResult_TrueNegativeBlock_Parity()
        {
            var filter = new LogFilter { FromBlock = 4, ToBlock = 4 };
            var result = await AssertParityAsync(filter);
            Assert.Empty(result);
        }

        [Fact]
        public async Task BloomFalsePositive_DroppedByExactFilter_BothStores()
        {
            var decoy = "0x" + new string('9', 40);
            var filter = new LogFilter { FromBlock = 49, ToBlock = 50, Addresses = new List<string> { AddrA, decoy } };

            var result = await AssertParityAsync(filter);

            Assert.Single(result);
            Assert.Equal(new BigInteger(49), result[0].BlockNumber);
            Assert.True(result[0].Address.IsTheSameAddress(AddrA));
        }

        [Fact]
        public async Task UnsetToBlock_ResolvesToTip_MatchesExplicitToBlockAtTip()
        {
            var implicitFilter = new LogFilter { FromBlock = 1, Addresses = new List<string> { AddrD } };
            var explicitFilter = new LogFilter { FromBlock = 1, ToBlock = 50, Addresses = new List<string> { AddrD } };

            var implicitResult = CanonicalSort(await _l1.GetLogsAsync(implicitFilter));
            var explicitResult = CanonicalSort(await _l1.GetLogsAsync(explicitFilter));

            Assert.Equal(explicitResult.Count, implicitResult.Count);
            for (int i = 0; i < implicitResult.Count; i++)
                AssertLogEqual(explicitResult[i], implicitResult[i]);
            Assert.NotEmpty(implicitResult);
        }

        [Fact]
        public async Task GetLogsByBlockNumberAsync_Parity()
        {
            var l1 = await _l1.GetLogsByBlockNumberAsync(11);
            var l2 = await _fixture.LogStore.GetLogsByBlockNumberAsync(11);

            Assert.Equal(l2.Count, l1.Count);
            for (int i = 0; i < l1.Count; i++) AssertLogEqual(l2[i], l1[i]);
            Assert.Equal(2, l1.Count);
        }

        [Fact]
        public async Task GetLogsByBlockHashAsync_Parity()
        {
            var blockHash = BlockHashOf(11);
            var l1 = await _l1.GetLogsByBlockHashAsync(blockHash);
            var l2 = await _fixture.LogStore.GetLogsByBlockHashAsync(blockHash);

            Assert.Equal(l2.Count, l1.Count);
            for (int i = 0; i < l1.Count; i++) AssertLogEqual(l2[i], l1[i]);
            Assert.Equal(2, l1.Count);
        }

        [Fact]
        public async Task GetLogsByTxHashAsync_Parity()
        {
            var txHash = TxHashOf(11, 0);
            var l1 = await _l1.GetLogsByTxHashAsync(txHash);
            var l2 = await _fixture.LogStore.GetLogsByTxHashAsync(txHash);

            Assert.Equal(l2.Count, l1.Count);
            for (int i = 0; i < l1.Count; i++) AssertLogEqual(l2[i], l1[i]);
            Assert.Single(l1);
        }

        [Fact]
        public async Task WriteMethods_AreNoOps()
        {
            await _l1.SaveLogsAsync(OneLog(AddrA, T0), TxHashOf(1, 0), BlockHashOf(1), 1, 0);
            await _l1.SaveManyLogsAsync(
                new List<(List<Log> Logs, byte[] TxHash, int TxIndex)> { (OneLog(AddrA, T0), TxHashOf(1, 0), 0) },
                BlockHashOf(1), 1);
            await _l1.SaveBlockBloomAsync(1, new byte[256]);
            await _l1.DeleteByBlockNumberAsync(1);

            var result = await _l1.GetLogsByBlockNumberAsync(1);
            Assert.Single(result);
        }
    }
}
