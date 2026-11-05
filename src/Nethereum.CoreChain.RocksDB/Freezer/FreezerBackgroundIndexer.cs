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

        private BackgroundRoundLoop _byHash;
        private BackgroundRoundLoop _render;
        private Task _byHashTaskForTests;
        private bool _closed;
        private Func<bool> _yieldToExecution;
        private Func<string> _describeYield;
        private readonly System.Diagnostics.Stopwatch _progressLog = System.Diagnostics.Stopwatch.StartNew();
        private readonly object _progressLogGate = new object();
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

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

        private bool YieldsToExecution => _yieldToExecution?.Invoke() == true;

        private bool ShouldPauseIndexing()
            => _pressureMonitor.ShouldPauseFreezerIndexing() || YieldsToExecution;

        private string DescribeIndexingPause()
            => YieldsToExecution ? _describeYield?.Invoke() : _pressureMonitor.DescribeFreezerIndexingBackpressure();

        public bool IsRunning => _backgroundEnabled
            && (_byHash?.IsStarted == true || _render?.IsStarted == true || _byHashTaskForTests != null);

        internal void SetPressureMonitorForTests(Stores.RocksDbWritePressureMonitor monitor) => _pressureMonitor = monitor;

        internal void SetByHashTaskForTests(Task trailer) => _byHashTaskForTests = trailer;

        [Obsolete("No longer used: frozen blocks are indexed by the background trailer only.")]
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
            if (!_backgroundEnabled || IsRunning)
            {
                CheckpointIngest();
                return;
            }
            DrainIndexingToTrueFreezerHead(ct);
            _freezerIndexIngestor?.Finish(ct);
        }

        private void DrainIndexingToTrueFreezerHead(CancellationToken ct)
        {
            if (_freezerAppend == null || ct.IsCancellationRequested) return;
            var head = _freezerAppend.Items;
            _builder.CatchUpByHash(head, ct);
            if (ct.IsCancellationRequested) return;
            _builder.RenderFilterMaps(ct, headOverride: head);
        }

        public void Start() => Start(null, null);

        internal void Start(Func<bool> yieldToExecution, Func<string> describeYield)
        {
            if (_closed || _freezerAppend == null || !_backgroundEnabled) return;
            if (_byHash != null || _render != null) return;

            _yieldToExecution = yieldToExecution;
            _describeYield = describeYield;
            _byHash = NewLoop("index-trailer byhash");
            _render = NewLoop("index-trailer render");
            _byHash.Start(ct =>
            {
                _builder.CatchUpByHash(_builder.ComputeIndexableBoundary(), ct);
                LogProgressIfDue();
            });
            _render.Start(ct =>
            {
                _builder.RenderFilterMaps(ct, headOverride: _builder.ComputeIndexableBoundary());
                LogProgressIfDue();
            });
        }

        private BackgroundRoundLoop NewLoop(string name)
            => new BackgroundRoundLoop(
                name, "background-indexer pausing (freezer files keep writing)", "background-indexer resuming",
                ShouldPauseIndexing, DescribeIndexingPause, PollInterval, PollInterval);

        public void CatchUpByHash(long head, CancellationToken ct) => _builder.CatchUpByHash(head, ct);

        public int RenderFilterMaps(CancellationToken ct = default, long? headOverride = null)
            => _builder.RenderFilterMaps(ct, headOverride);

        private void LogProgressIfDue()
        {
            lock (_progressLogGate)
            {
                if (_progressLog.Elapsed.TotalSeconds < 15) return;
                var lastError = _byHash?.LastError ?? _render?.LastError;
                var err = lastError == null ? "" : $" last_error=\"{lastError}\"";
                Console.Error.WriteLine(
                    $"snap.index.background freezer_head={_freezerAppend?.Items ?? 0} byhash_indexed={ByHashIndexedHead} log_rendered={LogIndexRenderedHead}{err}");
                _progressLog.Restart();
            }
        }

        public void Stop()
        {
            _byHash?.Stop();
            _render?.Stop();
            _byHash = null;
            _render = null;
        }

        internal async Task StopAsync()
        {
            var byHash = _byHash;
            var render = _render;
            _byHash = null;
            _render = null;
            if (byHash != null) await byHash.StopAsync().ConfigureAwait(false);
            if (render != null) await render.StopAsync().ConfigureAwait(false);
        }

        internal void Close()
        {
            _closed = true;
            Stop();
        }

        internal Task CloseAsync()
        {
            _closed = true;
            return StopAsync();
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
