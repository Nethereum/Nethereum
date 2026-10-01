using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Freezer
{
    public sealed class FreezerBackgroundIndexer
    {
        private readonly Nethereum.Freezer.IFrozenReadSource _freezerAppend;
        private Stores.RocksDbWritePressureMonitor _pressureMonitor;
        private readonly History.BulkIndexIngestor _freezerIndexIngestor;
        private readonly Stores.RocksDbFilterMapsStore _fmStore;
        private readonly FreezerBulkIndexBuilder _builder;
        private readonly bool _backgroundEnabled;

        private CancellationTokenSource _backgroundCts;
        private Task _byHashTask;
        private Task _renderTask;
        private volatile string _lastError;
        private readonly System.Diagnostics.Stopwatch _progressLog = System.Diagnostics.Stopwatch.StartNew();
        private readonly object _progressLogGate = new object();
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

        internal FreezerBackgroundIndexer(
            Nethereum.Freezer.IFrozenReadSource freezerAppend,
            Stores.RocksDbWritePressureMonitor pressureMonitor,
            FreezerCodecSet freezerCodecs,
            History.BulkIndexIngestor freezerIndexIngestor,
            Stores.RocksDbFilterMapsStore fmStore,
            FilterMapsIndexer fmIndexer,
            IFreezerIndexProgress progress,
            bool backgroundFreezeIndexing,
            int indexDegreeOfParallelism = 0)
        {
            _freezerAppend = freezerAppend;
            _pressureMonitor = pressureMonitor;
            _freezerIndexIngestor = freezerIndexIngestor;
            _fmStore = fmStore;
            _backgroundEnabled = backgroundFreezeIndexing;
            _builder = new FreezerBulkIndexBuilder(
                freezerAppend, freezerCodecs, freezerIndexIngestor, fmStore, fmIndexer, progress,
                byHashDegreeOfParallelism: indexDegreeOfParallelism,
                logDegreeOfParallelism: indexDegreeOfParallelism,
                shouldPause: ShouldPauseIndexing);
        }

        private bool ShouldPauseIndexing()
            => _pressureMonitor.ShouldPauseFreezerIndexing();

        private string DescribeIndexingPause()
            => _pressureMonitor.DescribeFreezerIndexingBackpressure();

        public bool IsRunning => _backgroundEnabled && (_byHashTask != null || _renderTask != null);

        internal void SetPressureMonitorForTests(Stores.RocksDbWritePressureMonitor monitor) => _pressureMonitor = monitor;

        public void IndexFrozenInline(IReadOnlyList<PersistableBlock> blocks, int startIndex, int count)
        {
            if (count <= 0) return;

            var entries = new List<(string Cf, byte[] Key, byte[] Value)>();
            ulong windowHead = 0;
            for (var i = startIndex; i < startIndex + count; i++)
            {
                var b = blocks[i];
                var number = (ulong)b.Header.BlockNumber.ToBigInteger();
                var txHashes = b.Transactions?.Select(tx => tx?.Hash).ToList();
                FreezerBulkIndexBuilder.AppendByHashIndexEntries(entries, number, b.Hash, txHashes);
                windowHead = number;
            }

            if (_freezerIndexIngestor.Accumulate(count, windowHead, entries))
                _freezerIndexIngestor.Checkpoint();
        }

        public void Finish(CancellationToken ct = default)
        {
            Stop();
            DrainIndexingToTrueFreezerHead(ct);
            _freezerIndexIngestor?.Finish();
        }

        private void DrainIndexingToTrueFreezerHead(CancellationToken ct)
        {
            if (_freezerAppend == null || ct.IsCancellationRequested) return;
            var head = _freezerAppend.Items;
            _builder.CatchUpByHash(head, ct);
            if (ct.IsCancellationRequested) return;
            _builder.RenderFilterMaps(ct, headOverride: head);
        }

        public void Start()
        {
            if (_freezerAppend == null || !_backgroundEnabled) return;
            if (_byHashTask != null || _renderTask != null) return;

            _backgroundCts = new CancellationTokenSource();
            var ct = _backgroundCts.Token;
            _byHashTask = Task.Factory.StartNew(
                () => RunLoopAsync(RunByHashRound, ct),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            _renderTask = Task.Factory.StartNew(
                () => RunLoopAsync(RunRenderRound, ct),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        private async Task RunLoopAsync(Func<CancellationToken, TimeSpan, TimeSpan> round, CancellationToken ct)
        {
            var backoff = TimeSpan.Zero;
            while (!ct.IsCancellationRequested)
            {
                if (!await WaitWhilePausedForCompactionAsync(ct).ConfigureAwait(false)) break;
                backoff = round(ct, backoff);
                LogProgressIfDue();
                if (!await WaitForPollIntervalOrBackoffAsync(backoff, ct).ConfigureAwait(false)) break;
            }
        }

        private async Task<bool> WaitWhilePausedForCompactionAsync(CancellationToken ct)
        {
            if (!ShouldPauseIndexing()) return true;

            Console.Error.WriteLine(
                $"[Nethereum.Freezer] background-indexer pausing (freezer files keep writing): {DescribeIndexingPause()}");
            try
            {
                while (ShouldPauseIndexing())
                    await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return false; }
            Console.Error.WriteLine("[Nethereum.Freezer] background-indexer resuming — freezer-history compaction debt drained.");
            return true;
        }

        private TimeSpan RunByHashRound(CancellationToken ct, TimeSpan backoff)
        {
            try
            {
                _builder.CatchUpByHash(_builder.ComputeIndexableBoundary(), ct);
                _lastError = null;
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                return BackOffAfterRoundFailure("byhash", ex, backoff);
            }
        }

        private TimeSpan RunRenderRound(CancellationToken ct, TimeSpan backoff)
        {
            try
            {
                _builder.RenderFilterMaps(ct, headOverride: _builder.ComputeIndexableBoundary());
                _lastError = null;
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                return BackOffAfterRoundFailure("render", ex, backoff);
            }
        }

        private TimeSpan BackOffAfterRoundFailure(string round, Exception ex, TimeSpan backoff)
        {
            _lastError = $"{ex.GetType().Name}: {ex.Message}";
            Console.Error.WriteLine($"[Nethereum.Freezer] index-trailer {round} round failed; backing off: {_lastError}");
            var next = backoff == TimeSpan.Zero ? TimeSpan.FromSeconds(1) : backoff + backoff;
            return next > MaxBackoff ? MaxBackoff : next;
        }

        public void CatchUpByHash(long head, CancellationToken ct) => _builder.CatchUpByHash(head, ct);

        public int RenderFilterMaps(CancellationToken ct = default, long? headOverride = null)
            => _builder.RenderFilterMaps(ct, headOverride);

        private void LogProgressIfDue()
        {
            lock (_progressLogGate)
            {
                if (_progressLog.Elapsed.TotalSeconds < 15) return;
                var err = _lastError == null ? "" : $" last_error=\"{_lastError}\"";
                Console.Error.WriteLine(
                    $"snap.index.background freezer_head={_freezerAppend?.Items ?? 0} byhash_indexed={ByHashIndexedHead} log_rendered={LogIndexRenderedHead}{err}");
                _progressLog.Restart();
            }
        }

        private async Task<bool> WaitForPollIntervalOrBackoffAsync(TimeSpan backoff, CancellationToken ct)
        {
            try
            {
                await Task.Delay(backoff > TimeSpan.Zero ? backoff : PollInterval, ct).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) { return false; }
        }

        public void Stop()
        {
            if (_byHashTask == null && _renderTask == null) return;
            _backgroundCts?.Cancel();
            try { _byHashTask?.Wait(); } catch { }
            try { _renderTask?.Wait(); } catch { }
            _backgroundCts?.Dispose();
            _backgroundCts = null;
            _byHashTask = null;
            _renderTask = null;
        }

        internal async Task StopAsync()
        {
            if (_byHashTask == null && _renderTask == null) return;
            _backgroundCts?.Cancel();
            try { if (_byHashTask != null) await _byHashTask.ConfigureAwait(false); } catch { }
            try { if (_renderTask != null) await _renderTask.ConfigureAwait(false); } catch { }
            _backgroundCts?.Dispose();
            _backgroundCts = null;
            _byHashTask = null;
            _renderTask = null;
        }

        public long ByHashIndexedHead => _builder.ByHashIndexedHead;
        public long LogIndexRenderedHead => _builder.LogIndexRenderedHead;
        public long LogRenderProgressBlock => _builder.LogRenderProgressBlock;

        public (ulong Previous, ulong New) AlignCursorToHead() => _builder.AlignCursorToHead();

        internal void SetByHashReindexCursorForTests(ulong itemNumber) => _builder.SetByHashCursor(itemNumber);

        internal void CheckpointIngest() => _freezerIndexIngestor?.Checkpoint();

        internal void FlushIfDirty() => _fmStore?.FlushIfDirty();
    }
}
