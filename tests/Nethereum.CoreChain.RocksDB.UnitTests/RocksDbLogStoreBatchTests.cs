using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbLogStoreBatchTests : IDisposable
    {
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"logbatch_{Guid.NewGuid():N}");
        private readonly RocksDbManager _manager;
        private readonly RocksDbLogStore _logStore;

        public RocksDbLogStoreBatchTests()
        {
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dataDir });
            _logStore = new RocksDbLogStore(_manager);
        }

        public void Dispose()
        {
            _manager.Dispose();
            if (Directory.Exists(_dataDir)) { try { Directory.Delete(_dataDir, true); } catch { } }
        }

        private static byte[] Fill(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static Log MakeLog(string address, byte data) => new Log
        {
            Address = address,
            Topics = new List<byte[]> { Fill(0x11) },
            Data = new byte[] { data },
        };

        [Fact]
        public async Task SaveManyLogsAsync_RoundTripsByBlockTxAndAddress()
        {
            var blockHash = Fill(0xBB);
            BigInteger blockNumber = 4242;
            var tx0 = Fill(0x10);
            var tx1 = Fill(0x11);
            var addrA = "0x" + new string('a', 40);
            var addrB = "0x" + new string('b', 40);

            var txLogs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>
            {
                (new List<Log> { MakeLog(addrA, 0xde), MakeLog(addrB, 0xbe) }, tx0, 0),
                (new List<Log> { MakeLog(addrA, 0xca) }, tx1, 1),
            };

            await _logStore.SaveManyLogsAsync(txLogs, blockHash, blockNumber);

            var byBlock = await _logStore.GetLogsByBlockNumberAsync(blockNumber);
            Assert.Equal(3, byBlock.Count);

            Assert.Equal(2, (await _logStore.GetLogsByTxHashAsync(tx0)).Count);
            Assert.Equal(1, (await _logStore.GetLogsByTxHashAsync(tx1)).Count);

            var byHash = await _logStore.GetLogsByBlockHashAsync(blockHash);
            Assert.Equal(3, byHash.Count);
        }

        [Fact]
        public async Task SaveManyLogsAsync_MatchesPerTxSaveLogsAsync()
        {
            var addr = "0x" + new string('c', 40);
            var blockHash = Fill(0xCC);
            BigInteger blockNumber = 999;
            var tx0 = Fill(0x20);
            var tx1 = Fill(0x21);
            var l0 = new List<Log> { MakeLog(addr, 0x01) };
            var l1 = new List<Log> { MakeLog(addr, 0x02), MakeLog(addr, 0x03) };

            var refDir = _dataDir + "_ref";
            using var refManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = refDir });
            var refLogStore = new RocksDbLogStore(refManager);
            await refLogStore.SaveLogsAsync(l0, tx0, blockHash, blockNumber, 0);
            await refLogStore.SaveLogsAsync(l1, tx1, blockHash, blockNumber, 1);
            var refByBlock = await refLogStore.GetLogsByBlockNumberAsync(blockNumber);

            await _logStore.SaveManyLogsAsync(
                new List<(List<Log>, byte[], int)> { (l0, tx0, 0), (l1, tx1, 1) }, blockHash, blockNumber);
            var batchByBlock = await _logStore.GetLogsByBlockNumberAsync(blockNumber);

            Assert.Equal(refByBlock.Count, batchByBlock.Count);
            Assert.Equal(3, batchByBlock.Count);

            refManager.Dispose();
            try { Directory.Delete(refDir, true); } catch { }
        }
    }
}
