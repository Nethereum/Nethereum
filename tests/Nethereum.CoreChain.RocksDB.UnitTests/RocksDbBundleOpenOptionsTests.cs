using System;
using System.IO;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbBundleOpenOptionsTests : IDisposable
    {
        private readonly string _root;

        public RocksDbBundleOpenOptionsTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "necc-openopts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [Fact]
        public void Open_Without_Options_Uses_Hash_State_Store()
        {
            using var bundle = RocksDbChainStoreBundle.Open(Path.Combine(_root, "hash"));

            Assert.Same(bundle.TrieNodes, bundle.StateTrieNodes);
        }

        [Fact]
        public void Open_With_PathKeyedState_Options_Uses_Path_State_Store()
        {
            var dir = Path.Combine(_root, "path");
            var opts = new RocksDbStorageOptions { PathKeyedState = true };

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: opts);

            Assert.NotSame(bundle.TrieNodes, bundle.StateTrieNodes);
        }

        [Fact]
        public void Open_Forces_DatabasePath_To_DataDir()
        {
            var dir = Path.Combine(_root, "forcedpath");
            var opts = new RocksDbStorageOptions { DatabasePath = Path.Combine(_root, "somewhere-else") };

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: opts);

            Assert.True(Directory.Exists(dir));
            Assert.Equal(dir, bundle.DataDir);
        }

        [Fact]
        public void Open_With_NodeHistory_Exceeding_State_History_Throws_At_Compose()
        {
            var dir = Path.Combine(_root, "crossbag");
            var storage = new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 1000,
            };
            var journal = new HistoricalStateOptions { MaxHistoryBlocks = 500 };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                RocksDbChainStoreBundle.Open(dir, journalOptions: journal, storageOptions: storage));
            Assert.Contains("TrieNodeHistoryBlocks", ex.Message);
        }

        [Fact]
        public void Open_With_NodeHistory_Within_State_History_Composes()
        {
            var dir = Path.Combine(_root, "crossbag-ok");
            var storage = new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 500,
            };
            var journal = new HistoricalStateOptions { MaxHistoryBlocks = 1000 };

            using var bundle = RocksDbChainStoreBundle.Open(dir, journalOptions: journal, storageOptions: storage);
            Assert.NotSame(bundle.TrieNodes, bundle.StateTrieNodes);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
