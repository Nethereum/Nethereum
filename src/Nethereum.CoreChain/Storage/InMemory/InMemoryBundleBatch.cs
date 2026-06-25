using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    /// <summary>
    /// In-memory <see cref="IBundleBatch"/>. Buffers every staged write in
    /// per-store lists; <see cref="CommitAsync"/> acquires the shared bundle
    /// semaphore and applies every buffered op, releasing the semaphore once
    /// the last write has landed. Atomic by gate: no other concurrent
    /// CommitAsync observes a half-applied batch.
    /// </summary>
    internal sealed class InMemoryBundleBatch : IBundleBatch
    {
        private readonly IChainStoreBundle _bundle;
        private readonly SemaphoreSlim _commitGate;
        private readonly List<Func<Task>> _ops = new();
        private bool _completed;

        public InMemoryBundleBatch(IChainStoreBundle bundle, SemaphoreSlim commitGate)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _commitGate = commitGate ?? throw new ArgumentNullException(nameof(commitGate));
        }

        public void PutHeader(BlockHeader header, byte[] blockHash)
        {
            EnsureOpen();
            _ops.Add(() => _bundle.Blocks.SaveAsync(header, blockHash));
        }

        public void PutUncles(byte[] blockHash, IList<BlockHeader> uncles)
        {
            EnsureOpen();
            _ops.Add(() => _bundle.Uncles.SaveAsync(blockHash, uncles ?? new List<BlockHeader>()));
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
                _ops.Add(() => _bundle.Transactions.SaveAsync(tx, blockHash, index, blockNumber));
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
            _ops.Add(() => _bundle.Receipts.SaveAsync(
                receipt, txHash, blockHash, blockNumber, txIndex,
                gasUsed, contractAddress, effectiveGasPrice));
        }

        public void SetLastFetchedHeader(ulong blockNumber)
        {
            EnsureOpen();
            _ops.Add(() => { _bundle.Metadata.SetLastFetchedHeader(blockNumber); return Task.CompletedTask; });
        }

        public void SetLastFetchedBody(ulong blockNumber)
        {
            EnsureOpen();
            _ops.Add(() => { _bundle.Metadata.SetLastFetchedBody(blockNumber); return Task.CompletedTask; });
        }

        public void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock)
        {
            EnsureOpen();
            _ops.Add(() =>
            {
                _bundle.Metadata.SetLastFetchedHeader(headerBlock);
                _bundle.Metadata.SetLastFetchedBody(bodyBlock);
                return Task.CompletedTask;
            });
        }

        public void Commit(ulong lastBlock, byte[] lastBlockHash)
        {
            EnsureOpen();
            _ops.Add(() => { _bundle.Metadata.Commit(lastBlock, lastBlockHash); return Task.CompletedTask; });
        }

        public void SaveSnapSyncState(SnapSyncState state)
        {
            EnsureOpen();
            _ops.Add(() => { _bundle.Metadata.SaveSnapSyncState(state); return Task.CompletedTask; });
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_completed) throw new InvalidOperationException("Bundle batch already completed.");
            ct.ThrowIfCancellationRequested();

            // Snapshot to avoid concurrent enqueue on the same batch from a
            // worker that is mis-using the API. Apply under the bundle gate
            // so concurrent CommitAsync calls don't interleave their ops.
            // SemaphoreSlim is not thread-affine: continuations after await
            // can resume on any thread and Release works regardless.
            var ops = _ops.ToArray();
            await _commitGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                foreach (var op in ops)
                {
                    ct.ThrowIfCancellationRequested();
                    await op().ConfigureAwait(false);
                }
            }
            finally
            {
                _commitGate.Release();
            }
            _ops.Clear();
            _completed = true;
        }

        public void Discard()
        {
            _ops.Clear();
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
