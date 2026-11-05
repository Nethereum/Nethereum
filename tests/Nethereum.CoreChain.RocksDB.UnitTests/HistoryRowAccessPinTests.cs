using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Freezer")]
    public class HistoryRowAccessPinTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"historyrows_{Guid.NewGuid():N}");

        public HistoryRowAccessPinTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        private static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);

        private static HistoryBlockWrite Block(ulong n, int txCount)
        {
            var txs = new List<HistoryTxWrite>();
            for (uint i = 0; i < txCount; i++)
            {
                var hash = new byte[32];
                hash[0] = (byte)n;
                hash[1] = (byte)i;
                txs.Add(new HistoryTxWrite { TxHash = hash, Tx = Enc($"tx{n}.{i}"), Receipt = Enc($"rcpt{n}.{i}") });
            }
            var blockHash = new byte[32];
            blockHash[0] = (byte)(0xB0 + n);
            return new HistoryBlockWrite
            {
                BlockNumber = n, BlockHash = blockHash, Header = Enc($"hdr{n}"), Meta = Enc($"meta{n}"), Transactions = txs
            };
        }

        [Fact]
        public async Task Given_NoBlocksStored_When_TheTipIsRequested_Then_EveryStoreReturnsNull()
        {
            var rawDir = Path.Combine(_root, "raw");
            var cache = RocksProfiles.CreateSharedCache(16 * 1024 * 1024);
            using var db = RocksDb.Open(RocksProfiles.Db(2, 32 * 1024 * 1024), rawDir, RocksProfiles.BuildColumnFamilies(cache));
            using var manager = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = Path.Combine(_root, "managed"), PromotionEnabled = true
            });

            Assert.Null(new RocksDbHistoryStore(db).GetTip());
            Assert.Null(await new RocksDbBlockStore(manager).GetLatestAsync());
            Assert.Equal(-1, (int)await new RocksDbBlockStore(manager).GetHeightAsync());
            Assert.Null(new RocksDbHotBlockWindowStore(manager, 128).TryGetLatestNumber());
            Assert.Empty(await new HistoryBloomScanLogStore(manager).GetLogsAsync(new LogFilter()));
        }

        [Fact]
        public void Given_TxRowsOfTwoAdjacentBlocks_When_OneBlocksRowsAreScanned_Then_OnlyThatBlocksRowsAreReturned()
        {
            var dir = Path.Combine(_root, "scan");
            var cache = RocksProfiles.CreateSharedCache(16 * 1024 * 1024);
            using var db = RocksDb.Open(RocksProfiles.Db(2, 32 * 1024 * 1024), dir, RocksProfiles.BuildColumnFamilies(cache));
            var store = new RocksDbHistoryStore(db);
            store.AppendBlock(Block(4, 2));
            store.AppendBlock(Block(5, 3));
            store.AppendBlock(Block(6, 1));

            var fiveRows = store.GetBlockTransactions(5).Select(row => Encoding.UTF8.GetString(row)).ToArray();
            var fourRows = store.GetBlockTransactions(4).Select(row => Encoding.UTF8.GetString(row)).ToArray();

            Assert.Equal(new[] { "tx5.0", "tx5.1", "tx5.2" }, fiveRows);
            Assert.Equal(new[] { "tx4.0", "tx4.1" }, fourRows);
        }
    }
}
