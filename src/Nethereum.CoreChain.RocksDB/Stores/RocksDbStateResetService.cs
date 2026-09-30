using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class RocksDbStateResetService
    {
        private readonly RocksDbManager _rocks;
        private readonly IChainMetadataStore _metadata;

        public RocksDbStateResetService(RocksDbManager rocks, IChainMetadataStore metadata)
        {
            _rocks = rocks;
            _metadata = metadata;
        }

        public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default)
        {
            _metadata.ClearSnapSyncState();
            _metadata.ClearCommittedHead();
            _metadata.ClearDeferredHealAccountsBlob();
            _metadata.ClearDeferredHealCodeBlob();
            _metadata.ClearAllDeferredStorageDebts();
            WipeColumnFamilies(RocksDbManager.StateTrieCfs);
            WipeColumnFamilies(RocksDbManager.BinaryTrieCfs);
            WipeColumnFamilies(RocksDbManager.StateHistoryCfs);
            WipeColumnFamilies(RocksDbManager.NodeHistoryCfs);
            _rocks.Flush();
            return Task.CompletedTask;
        }

        public Task ResetStateOnlyAsync(CancellationToken ct = default)
        {
            _metadata.ResetForStateRebuild();
            WipeColumnFamilies(RocksDbManager.StateTrieCfs);
            WipeColumnFamilies(RocksDbManager.NodeHistoryCfs);
            WipeColumnFamilies(RocksDbManager.BinaryTrieCfs);
            WipeColumnFamilies(RocksDbManager.LogReceiptCfs);
            WipeColumnFamilies(RocksDbManager.StateHistoryCfs);
            WipeColumnFamilies(RocksDbManager.AuxiliaryStateCfs);
            _rocks.Flush();

            foreach (var cf in RocksDbManager.StateTrieCfs) _rocks.CompactColumnFamily(cf);
            foreach (var cf in RocksDbManager.NodeHistoryCfs) _rocks.CompactColumnFamily(cf);
            return Task.CompletedTask;
        }

        private void WipeColumnFamilies(IReadOnlyList<string> cfs)
        {
            foreach (var cf in cfs) _rocks.WipeColumnFamily(cf);
        }
    }
}
