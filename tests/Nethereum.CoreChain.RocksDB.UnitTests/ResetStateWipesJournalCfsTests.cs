using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class ResetStateWipesJournalCfsTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private readonly RocksDbChainStoreBundle _bundle;

        public ResetStateWipesJournalCfsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-reset-journal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir, PathKeyedState = true, TrieNodeHistoryBlocks = 128, TrieNodeHistoryIndex = true,
            });
            _bundle = RocksDbChainStoreBundle.FromManager(_mgr, _dir, HistoricalStateOptions.Default, ownsManager: false);
        }

        [Fact]
        public async Task ResetStateOnly_ClearsFlatState_TrieNodes_AndNodeHistoryJournalCfs()
        {
            await _bundle.State.SaveAccountAsync(Addr(1), new Account { Balance = 100, Nonce = 1 });
            PutRaw(RocksDbManager.CF_NODE_HISTORY, new byte[] { 1, 2, 3 });
            PutRaw(RocksDbManager.CF_NODE_HISTORY_INDEX, new byte[] { 4, 5, 6 });
            PutRaw(RocksDbManager.CF_STATE_ROOT_INDEX, new byte[] { 7, 8, 9 });
            _bundle.Metadata.MarkGenesisLoaded();
            _mgr.Flush();

            Assert.True(Count(RocksDbManager.CF_STATE_ACCOUNTS) > 0);
            Assert.True(Count(RocksDbManager.CF_NODE_HISTORY) > 0);
            Assert.True(Count(RocksDbManager.CF_NODE_HISTORY_INDEX) > 0);
            Assert.True(Count(RocksDbManager.CF_STATE_ROOT_INDEX) > 0);

            await _bundle.ResetStateOnlyAsync();

            Assert.Equal(0, Count(RocksDbManager.CF_STATE_ACCOUNTS));
            Assert.Equal(0, Count(RocksDbManager.CF_STATE_TRIE_ACCOUNT));
            Assert.Equal(0, Count(RocksDbManager.CF_STATE_TRIE_STORAGE));
            Assert.Equal(0, Count(RocksDbManager.CF_NODE_HISTORY));
            Assert.Equal(0, Count(RocksDbManager.CF_NODE_HISTORY_INDEX));
            Assert.Equal(0, Count(RocksDbManager.CF_STATE_ROOT_INDEX));
            Assert.False(_bundle.Metadata.IsGenesisLoaded());
            Assert.Equal(0UL, _bundle.Metadata.GetLastBlock());
        }

        private void PutRaw(string cf, byte[] key)
        {
            using var batch = _mgr.CreateWriteBatch();
            batch.Put(key, new byte[] { 0xff }, _mgr.GetColumnFamily(cf));
            _mgr.Write(batch);
        }

        private int Count(string cf)
        {
            using var it = _mgr.CreateIterator(cf);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        public void Dispose()
        {
            _bundle?.Dispose();
            _mgr?.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
