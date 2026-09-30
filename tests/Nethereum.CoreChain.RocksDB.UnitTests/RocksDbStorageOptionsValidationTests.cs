using System;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbStorageOptionsValidationTests
    {
        [Fact]
        public void Defaults_Are_Off_And_Valid()
        {
            var opts = new RocksDbStorageOptions();

            Assert.Equal(-1, opts.TrieNodeHistoryBlocks);
            Assert.False(opts.TrieNodeHistoryIndex);
            Assert.False(opts.PathKeyedState);

            opts.Validate();
        }

        [Fact]
        public void TrieNodeHistoryBlocks_Below_MinusOne_Throws()
        {
            var opts = new RocksDbStorageOptions { TrieNodeHistoryBlocks = -2 };
            var ex = Assert.Throws<InvalidOperationException>(() => opts.Validate());
            Assert.Contains("TrieNodeHistoryBlocks", ex.Message);
        }

        [Fact]
        public void Index_Without_History_Throws()
        {
            var opts = new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = -1,
                TrieNodeHistoryIndex = true,
            };
            var ex = Assert.Throws<InvalidOperationException>(() => opts.Validate());
            Assert.Contains("TrieNodeHistoryIndex", ex.Message);
        }

        [Fact]
        public void History_Without_PathKeyedState_Throws()
        {
            var opts = new RocksDbStorageOptions
            {
                PathKeyedState = false,
                TrieNodeHistoryBlocks = 128,
            };
            var ex = Assert.Throws<InvalidOperationException>(() => opts.Validate());
            Assert.Contains("PathKeyedState", ex.Message);
        }

        [Fact]
        public void PathKeyed_Archive_History_Combo_Is_Valid()
        {
            var opts = new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 0,
                TrieNodeHistoryIndex = true,
            };
            opts.Validate();

            var windowed = new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 1024,
                TrieNodeHistoryIndex = false,
            };
            windowed.Validate();
        }

        [Fact]
        public void PathKeyed_Without_NodeHistory_Is_Valid()
        {
            var opts = new RocksDbStorageOptions { PathKeyedState = true };
            opts.Validate();
        }
    }
}
