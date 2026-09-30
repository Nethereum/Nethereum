using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB
{
    public sealed partial class RocksDbChainStoreBundle
    {
        public void CheckpointBulk()
        {
            _bulkSave?.Checkpoint();
            _freezerIndexer?.CheckpointIngest();
        }

        public void FinishBulkSync() => _bulkSave?.Finish();

        public string ResolveCheckpointSnapshotPath(ulong blockNumber)
            => _checkpointManager.ResolveCheckpointSnapshotPath(blockNumber);
        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "chain-store-bundle", "RocksDbChainStoreBundle.SaveCheckpointAsync — checkpoint the committed state")]
        public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
            => _checkpointManager.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
        public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default)
            => _checkpointManager.ListCheckpointsAsync(ct);
        public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
            => _checkpointManager.DeleteCheckpointAsync(blockNumber, ct);
        public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
            => _checkpointManager.RestoreCheckpointAsync(blockNumber, ct);
        public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default)
            => _checkpointManager.ExportDatabaseAsync(outputPath, ct);
    }
}
