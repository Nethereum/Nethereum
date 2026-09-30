using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbLogStoreBlockWideIndexTests : IDisposable
    {
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"logblockwide_{Guid.NewGuid():N}");
        private readonly RocksDbManager _manager;
        private readonly RocksDbLogStore _logStore;

        private const int BlockNumber = 1000;
        private static readonly byte[] BlockHash = Enumerable.Repeat((byte)0xBB, 32).ToArray();
        private static readonly string AddrA = "0x" + new string('a', 40);
        private static readonly string AddrZ = "0x" + new string('9', 40);

        public RocksDbLogStoreBlockWideIndexTests()
        {
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dataDir });
            _logStore = new RocksDbLogStore(_manager);
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _manager.Dispose();
            if (Directory.Exists(_dataDir)) { try { Directory.Delete(_dataDir, true); } catch { } }
        }

        private static readonly byte[] Topic = Enumerable.Repeat((byte)0x11, 32).ToArray();

        private static Log MakeLog(string address, byte data) => new Log
        {
            Address = address,
            Topics = new List<byte[]> { Topic },
            Data = new byte[] { data },
        };

        private static byte[] TxHashOf(int txIndex) => Enumerable.Repeat((byte)txIndex, 32).ToArray();

        private async Task SeedAsync()
        {
            var txLogs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>
            {
                (new List<Log> { MakeLog(AddrA, 0x00), MakeLog(AddrA, 0x01) }, TxHashOf(0), 0),
                (new List<Log> { MakeLog(AddrA, 0x10) }, TxHashOf(1), 1),
                (new List<Log> { MakeLog(AddrA, 0x20) }, TxHashOf(2), 256),
                (new List<Log> { MakeLog(AddrZ, 0x30) }, TxHashOf(3), 257),
            };
            await _logStore.SaveManyLogsAsync(txLogs, BlockHash, BlockNumber);

            var bloom = new LogBloomFilter();
            bloom.AddAddress(AddrA);
            bloom.AddAddress(AddrZ);
            bloom.AddTopic(Topic);
            await _logStore.SaveBlockBloomAsync(BlockNumber, bloom.Data);
        }

        [Fact]
        public async Task Given_TxIndexAcross256_When_GetLogsMatchAll_Then_LogIndexIsBlockWideInTxOrder()
        {
            var logs = await _logStore.GetLogsAsync(new LogFilter { FromBlock = BlockNumber, ToBlock = BlockNumber });

            Assert.Equal(new[] { 0, 0, 1, 256, 257 }, logs.Select(l => l.TransactionIndex).ToArray());
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, logs.Select(l => l.LogIndex).ToArray());
        }

        [Fact]
        public async Task Given_FilterMatchesOnlyLastTx_When_GetLogs_Then_LogIndexIsAbsoluteNotZero()
        {
            var filter = new LogFilter
            {
                FromBlock = BlockNumber,
                ToBlock = BlockNumber,
                Addresses = new List<string> { AddrZ }
            };

            var logs = await _logStore.GetLogsAsync(filter);

            var only = Assert.Single(logs);
            Assert.Equal(257, only.TransactionIndex);
            Assert.Equal(4, only.LogIndex);
        }

        [Fact]
        public async Task Given_TxIndexAcross256_When_GetLogsByBlockHash_Then_BlockWideAndOrdered()
        {
            var logs = await _logStore.GetLogsByBlockHashAsync(BlockHash);

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, logs.Select(l => l.LogIndex).ToArray());
        }
    }
}
