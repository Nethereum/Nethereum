using System.Collections.Generic;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PersistedColumnFamilyNamesTests
    {
        [Fact]
        public void NodeHistoryColumnFamily_OnDiskName_IsStable()
        {
            Assert.Equal("node_band_log", RocksDbManager.CF_NODE_HISTORY);
            Assert.Equal("node_history_index", RocksDbManager.CF_NODE_HISTORY_INDEX);
        }

        [Fact]
        public void StateWipeGroups_HaveTheExpectedMembership()
        {
            Assert.Equal(
                new[] { "state_accounts", "state_storage", "state_code", "trie_nodes", "state_trie_account", "state_trie_storage" },
                (IEnumerable<string>)RocksDbManager.StateTrieCfs);
            Assert.Equal(
                new[] { "binary_trie_nodes", "binary_trie_depth_idx", "binary_trie_addr_stems" },
                (IEnumerable<string>)RocksDbManager.BinaryTrieCfs);
            Assert.Equal(
                new[] { "state_history_accounts", "state_history_storage", "state_history_block_index", "state_history_meta" },
                (IEnumerable<string>)RocksDbManager.StateHistoryCfs);
            Assert.Equal(
                new[] { "node_band_log", "node_history_index", "state_root_index" },
                (IEnumerable<string>)RocksDbManager.NodeHistoryCfs);
            Assert.Equal(
                new[] { "receipts", "logs", "log_by_block", "log_by_address", "log_by_tx", "receipt_by_block", "block_blooms" },
                (IEnumerable<string>)RocksDbManager.LogReceiptCfs);
            Assert.Equal(
                new[] { "filters", "msg_results", "msg_results_by_leaf" },
                (IEnumerable<string>)RocksDbManager.AuxiliaryStateCfs);
        }
    }
}
