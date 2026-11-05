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
        private static readonly TimeSpan FreezerRecheckInterval = TimeSpan.FromMinutes(1);

        private Freezer.BackgroundRoundLoop _freezerJob;
        private const int JobsNotStarted = 0;
        private const int JobsStarted = 1;
        private const int JobsDisposing = 2;
        private int _jobsState;
        private int _recoveryWaiting;
        private readonly object _jobsLock = new object();
        private readonly SemaphoreSlim _promotionCursorGate = new SemaphoreSlim(1, 1);
        private volatile bool _yieldArmed;
        private readonly object _tipCacheGate = new object();
        private long _tipCachedAtTicks;
        private ulong _cachedTrustedTip;

        private void StartFreezerJob()
        {
            if (_promotionService == null && _freezerPromotionDriver == null) return;

            _freezerJob = new Freezer.BackgroundRoundLoop(
                "freezer-promotion", "freezer-promotion pausing", "freezer-promotion resuming",
                ShouldYieldToExecution, DescribeYieldToExecution, TimeSpan.FromSeconds(1), FreezerRecheckInterval);
            _freezerJob.Start(RunFreezerRound);
        }

        private void RunFreezerRound(CancellationToken ct)
        {
            _promotionService?.PromoteDurableBlocks(
                Metadata.GetDurableStateBlock(), ct, ShouldYieldToExecution, RecoveryIsWaiting, _promotionCursorGate);
            _freezerPromotionDriver?.Drive(ct, ShouldYieldToExecution, RecoveryIsWaiting);
        }

        private bool RecoveryIsWaiting() => Volatile.Read(ref _recoveryWaiting) > 0;

        private async Task<T> UnderPromotionCursorGateAsync<T>(Func<Task<T>> recovery, CancellationToken ct)
        {
            Interlocked.Increment(ref _recoveryWaiting);
            try
            {
                await _promotionCursorGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    return await recovery().ConfigureAwait(false);
                }
                finally
                {
                    _promotionCursorGate.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _recoveryWaiting);
            }
        }

        private Freezer.BackgroundRoundLoop BeginDispose()
        {
            lock (_jobsLock)
            {
                _jobsState = JobsDisposing;
                return _freezerJob;
            }
        }

        private ulong CachedTrustedTip()
        {
            lock (_tipCacheGate)
            {
                var now = Environment.TickCount64;
                if (_tipCachedAtTicks != 0 && now - _tipCachedAtTicks < 1_000) return _cachedTrustedTip;
                var state = Metadata.GetHeaderSyncState();
                _cachedTrustedTip = state == null ? 0UL : HeaderSubchains.TrustedTip(state);
                _tipCachedAtTicks = now == 0 ? 1 : now;
                return _cachedTrustedTip;
            }
        }

        public ulong? PromotionFloor(ulong currentHead)
        {
            if (_promotionService == null || _promotionMaxHistoryBlocks == 0) return null;
            var window = currentHead > _promotionMaxHistoryBlocks ? currentHead - _promotionMaxHistoryBlocks : 0UL;
            var cursor = (Metadata as RocksDbChainMetadataStore)?.GetPromotionCursor() ?? 0UL;
            return Math.Max(window, cursor);
        }
    }
}
