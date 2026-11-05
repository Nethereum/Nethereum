using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            => ResetSnapBootstrapStateAsync(null, ct);

        public Task ResetSnapBootstrapStateAsync(Action<string> progress, CancellationToken ct = default)
        {
            var total = Stopwatch.StartNew();
            progress?.Invoke("wipe started: snap-sync metadata, then the state / trie / state-history column families");

            var step = Stopwatch.StartNew();
            _metadata.ClearSnapSyncState();
            _metadata.ClearCommittedHead();
            _metadata.ClearDeferredHealAccountsBlob();
            _metadata.ClearDeferredHealCodeBlob();
#pragma warning disable CS0618
            _metadata.ClearAllDeferredStorageDebts();
#pragma warning restore CS0618
            progress?.Invoke($"snap-sync metadata cleared in {step.Elapsed}");

            WipeColumnFamilies(RocksDbManager.StateTrieCfs, progress, ct);
            WipeColumnFamilies(RocksDbManager.BinaryTrieCfs, progress, ct);
            WipeColumnFamilies(RocksDbManager.StateHistoryCfs, progress, ct);
            WipeColumnFamilies(RocksDbManager.NodeHistoryCfs, progress, ct);
            _rocks.Flush();

            step.Restart();
            progress?.Invoke($"compacting {RocksDbManager.CF_METADATA} to drop the cleared rows' delete tombstones");
            _rocks.CompactColumnFamily(RocksDbManager.CF_METADATA);
            progress?.Invoke($"compacted {RocksDbManager.CF_METADATA} in {step.Elapsed}");

            progress?.Invoke($"wipe done in {total.Elapsed}");
            return Task.CompletedTask;
        }

        public Task ResetStateOnlyAsync(CancellationToken ct = default)
        {
            _metadata.ResetForStateRebuild();
            WipeColumnFamilies(RocksDbManager.StateTrieCfs, null, ct);
            WipeColumnFamilies(RocksDbManager.NodeHistoryCfs, null, ct);
            WipeColumnFamilies(RocksDbManager.BinaryTrieCfs, null, ct);
            WipeColumnFamilies(RocksDbManager.LogReceiptCfs, null, ct);
            WipeColumnFamilies(RocksDbManager.StateHistoryCfs, null, ct);
            WipeColumnFamilies(RocksDbManager.AuxiliaryStateCfs, null, ct);
            _rocks.Flush();

            foreach (var cf in RocksDbManager.StateTrieCfs) _rocks.CompactColumnFamily(cf);
            foreach (var cf in RocksDbManager.NodeHistoryCfs) _rocks.CompactColumnFamily(cf);
            return Task.CompletedTask;
        }

        private void WipeColumnFamilies(IReadOnlyList<string> cfs, Action<string> progress, CancellationToken ct)
        {
            foreach (var cf in cfs)
            {
                ct.ThrowIfCancellationRequested();
                var keys = ReadProperty(cf, "rocksdb.estimate-num-keys");
                var sstBytes = ReadProperty(cf, "rocksdb.total-sst-files-size");
                var step = Stopwatch.StartNew();
                _rocks.WipeColumnFamily(cf);
                progress?.Invoke($"{cf} dropped (~{keys} keys, {sstBytes} SST bytes) in {step.Elapsed}");
            }
        }

        private string ReadProperty(string cf, string property)
        {
            using var lease = _rocks.Lease();
            return lease.Database.GetProperty(property, _rocks.GetColumnFamily(cf)) ?? "?";
        }
    }
}
