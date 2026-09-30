using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB
{
    internal sealed class RocksDbBundleBatch : IBundleBatch
    {
        private static readonly WriteOptions SyncWrite = new WriteOptions().SetSync(true);

        private readonly IChainStoreBundle _bundle;
        private readonly RocksDbManager _rocks;
        private readonly ISnapSyncStateEncoder _snapSyncEncoder;
        private readonly bool _syncFsync;

        private readonly List<Func<Task>> _dataOps = new();

        private readonly List<Action<WriteBatch, ColumnFamilyHandle>> _metaOps = new();

        private bool _completed;

        public RocksDbBundleBatch(
            IChainStoreBundle bundle,
            RocksDbManager rocks,
            ISnapSyncStateEncoder snapSyncEncoder,
            bool syncFsync)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
            _snapSyncEncoder = snapSyncEncoder ?? SnapSyncStateRlpEncoder.Instance;
            _syncFsync = syncFsync;
        }

        public void PutHeader(BlockHeader header, byte[] blockHash)
        {
            EnsureOpen();
            _dataOps.Add(() => _bundle.Blocks.SaveAsync(header, blockHash));
        }

        public void PutUncles(byte[] blockHash, IList<BlockHeader> uncles)
        {
            EnsureOpen();
            _dataOps.Add(() => _bundle.Uncles.SaveAsync(blockHash, uncles ?? new List<BlockHeader>()));
        }

        public void PutTransactions(
            byte[] blockHash,
            BigInteger blockNumber,
            IList<ISignedTransaction> transactions)
        {
            EnsureOpen();
            if (transactions == null) return;
            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                int index = i;
                _dataOps.Add(() => _bundle.Transactions.SaveAsync(tx, blockHash, index, blockNumber));
            }
        }

        public void PutReceipt(
            Receipt receipt,
            byte[] txHash,
            byte[] blockHash,
            BigInteger blockNumber,
            int txIndex,
            BigInteger gasUsed,
            string contractAddress,
            BigInteger effectiveGasPrice)
        {
            EnsureOpen();
            _dataOps.Add(() => _bundle.Receipts.SaveAsync(
                receipt, txHash, blockHash, blockNumber, txIndex,
                gasUsed, contractAddress, effectiveGasPrice));
        }

        public void SetLastFetchedHeader(ulong blockNumber)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedHeader, RocksDbManager.Write64BE(blockNumber), cf));
        }

        public void SetLastFetchedBody(ulong blockNumber)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedBody, RocksDbManager.Write64BE(blockNumber), cf));
        }

        public void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
            {
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedHeader, RocksDbManager.Write64BE(headerBlock), cf);
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastFetchedBody, RocksDbManager.Write64BE(bodyBlock), cf);
            });
        }

        public void Commit(ulong lastBlock, byte[] lastBlockHash)
        {
            EnsureOpen();
            _metaOps.Add((batch, cf) =>
            {
                batch.Put(RocksDbChainMetadataStore.MetaKeys.LastBlock, RocksDbManager.Write64BE(lastBlock), cf);
                if (lastBlockHash != null && lastBlockHash.Length == 32)
                    batch.Put(RocksDbChainMetadataStore.MetaKeys.LastBlockHash, lastBlockHash, cf);
            });
        }

        public void SaveSnapSyncState(SnapSyncState state)
        {
            EnsureOpen();
            if (state == null) throw new ArgumentNullException(nameof(state));
            var blob = _snapSyncEncoder.Encode(state);
            _metaOps.Add((batch, cf) =>
                batch.Put(RocksDbChainMetadataStore.MetaKeys.SnapSyncState, blob, cf));
        }

        public void UpsertDeferredStorageDebt(DeferredStorageDebt debt)
        {
            EnsureOpen();
            DeferredStorageDebt.Validate(debt);
            var key = RocksDbChainMetadataStore.DeferredStorageDebtKey(debt.AccountHash, debt.DiscoveredStorageRoot);
            var blob = DeferredStorageDebtCodec.Encode(debt);
            _metaOps.Add((batch, cf) => batch.Put(key, blob, cf));
        }

        public void SaveDeferredHealCodeBlob(byte[] blob)
        {
            EnsureOpen();
            var value = blob ?? Array.Empty<byte>();
            _metaOps.Add((batch, cf) =>
                batch.Put(RocksDbChainMetadataStore.MetaKeys.DeferredHealCode, value, cf));
        }
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_completed) throw new InvalidOperationException("Bundle batch already completed.");
            ct.ThrowIfCancellationRequested();

            foreach (var op in _dataOps)
            {
                ct.ThrowIfCancellationRequested();
                await op().ConfigureAwait(false);
            }

            if (_metaOps.Count > 0)
            {
                var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
                using var batch = _rocks.CreateWriteBatch();
                foreach (var op in _metaOps) op(batch, cf);
                var opts = _syncFsync ? SyncWrite : null;
                _rocks.Write(batch, opts);
            }

            _dataOps.Clear();
            _metaOps.Clear();
            _completed = true;
        }

        public void Discard()
        {
            _dataOps.Clear();
            _metaOps.Clear();
            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed) Discard();
        }

        private void EnsureOpen()
        {
            if (_completed) throw new InvalidOperationException("Bundle batch already committed or discarded.");
        }
    }
}
