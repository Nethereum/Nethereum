using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class SnapRunContext
    {
        public IChainStoreBundle Bundle { get; }
        public ISnapPeer Peer { get; }
        public BlockHeader Pivot { get; }
        public byte[] PivotHash { get; }
        public SnapRunOptions Options { get; }
        public ILogger Logger { get; }
        public PivotWatch Pivots { get; }

        public IFetchRequestScheduler Scheduler => Options.Scheduler;
        public IPeerPool Pool => Options.Pool;
        public SnapSyncMetrics Metrics => Options.Metrics;
        public IChainActivations Activations => Options.Activations;
        public Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> PivotRefresher => Options.PivotRefresher;
        public bool ExternalHeaderFollow => Options.ExternalHeaderFollow;
        public bool BackfillOnly => Options.BackfillOnly;
        public bool BalHealEnabled => Options.BalHealEnabled;

        private SnapRunContext(
            IChainStoreBundle bundle, ISnapPeer peer, BlockHeader pivot, byte[] pivotHash, SnapRunOptions options,
            SnapBootstrapper.RollingPivot rollingPivot, ILogger logger)
        {
            Bundle = bundle;
            Peer = peer;
            Pivot = pivot;
            PivotHash = pivotHash;
            Options = options;
            Logger = logger;
            Pivots = new PivotWatch(rollingPivot, options.PivotRefresher, options.Scheduler, logger);
        }

        public static SnapRunContext Create(
            IChainStoreBundle bundle, ISnapPeer peer, BlockHeader pivot, byte[] pivotHash, ILogger logger, SnapRunOptions options)
        {
            options ??= new SnapRunOptions();
            if (bundle is null) throw new ArgumentNullException(nameof(bundle));
            if (peer is null) throw new ArgumentNullException(nameof(peer));
            if (pivot is null) throw new ArgumentNullException(nameof(pivot));
            if (pivotHash is null || pivotHash.Length != 32)
                throw new ArgumentException("pivotHash must be 32 bytes", nameof(pivotHash));
            if (pivot.StateRoot is null || pivot.StateRoot.Length != 32)
                throw new ArgumentException("pivot.StateRoot must be 32 bytes", nameof(pivot));
            logger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            var rollingPivot = options.RollingPivot ?? new SnapBootstrapper.RollingPivot(pivot, pivotHash);
            return new SnapRunContext(bundle, peer, pivot, pivotHash, options, rollingPivot, logger);
        }

        public SnapSyncClient BuildClient(ISnapSyncSink sink, IBulkFlatStateSink bulkFlat)
        {
            var client = new SnapSyncClient(Peer, sink, logger: Logger, metrics: Metrics)
            {
                RootRefreshIntervalMs = Options.RootRefreshIntervalMs,
                OnPivotRolled = () => Scheduler?.OnTargetRootChanged(),
            };
            if (Options.AccountConcurrency.HasValue)
                client.AccountConcurrency = Options.AccountConcurrency.Value;
            if (Options.LargeContractConcurrency.HasValue)
                client.LargeContractConcurrency = Options.LargeContractConcurrency.Value;
            if (bulkFlat != null)
            {
                client.CheckpointBytesThreshold = 256UL * 1024 * 1024;
                client.FlushBulkFlatBeforeCheckpoint = bulkFlat.Flush;
                Logger.LogInformation(
                    "snap.flat.sst enabled — flat rows via sorted SST ingestion; checkpoint cadence raised to 256MB");
            }
            if (Bundle is IStateWriteBackpressure stateValve)
                client.StateWriteBackpressure =
                    () => stateValve.ShouldPauseStateWrites() ? stateValve.DescribeStateBackpressure() : null;
            return client;
        }

        public Action<SnapSyncClient.SnapSyncCheckpoint> CheckpointSink(Func<SnapBootstrapper.PivotState> checkpointPivot)
            => checkpoint => PersistCheckpoint(checkpoint, checkpointPivot());

        private void PersistCheckpoint(SnapSyncClient.SnapSyncCheckpoint checkpoint, SnapBootstrapper.PivotState live)
        {
            var withPivot = checkpoint.State with
            {
                PivotBlockNumber = (ulong)live.Header.BlockNumber,
                PivotBlockHash = live.Hash,
            };
            Phase3Heal.PersistAccountsNeedingHeal(Bundle.Metadata, checkpoint.AccountsNeedingHeal);
            using var batch = Bundle.BeginBatch();
            if (checkpoint.DeferredCodeHashes != null && checkpoint.DeferredCodeHashes.Count > 0)
            {
                var union = NeedsHealCodeCodec.DecodeSet(Bundle.Metadata.GetDeferredHealCodeBlob());
                foreach (var h in checkpoint.DeferredCodeHashes) union.Add(h);
                batch.SaveDeferredHealCodeBlob(NeedsHealCodeCodec.EncodeSet(union));
            }
            batch.SaveSnapSyncState(withPivot);
            batch.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
