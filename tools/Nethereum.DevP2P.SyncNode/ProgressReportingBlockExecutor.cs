using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// IBlockExecutor decorator that emits a one-line progress report every
    /// <paramref name="_reportEvery"/> blocks (default 50) so the operator
    /// can watch sync advance through millions of blocks. Also emits a
    /// distinct line on every state-root mismatch so divergences stand out
    /// in the log stream. Zero cost between reports — just an int increment
    /// and a modulo.
    /// </summary>
    internal sealed class ProgressReportingBlockExecutor : IBlockExecutor
    {
        private readonly IBlockExecutor _inner;
        private readonly IPeerPool _pool;
        private readonly int _reportEvery;
        private readonly ILogger<ProgressReportingBlockExecutor> _logger;
        private readonly Stopwatch _wall;
        private long _blocksSinceLastReport;
        private long _blocksTotal;
        private long _mismatchesTotal;
        private long _lastReportBlockNumber;
        private long _lastReportElapsedMs;
        private DateTime _started;

        public ProgressReportingBlockExecutor(
            IBlockExecutor inner,
            IPeerPool pool,
            int reportEvery,
            ILogger<ProgressReportingBlockExecutor> logger = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _pool = pool;
            _reportEvery = reportEvery <= 0 ? 50 : reportEvery;
            _logger = logger ?? NullLogger<ProgressReportingBlockExecutor>.Instance;
            _wall = Stopwatch.StartNew();
            _started = DateTime.UtcNow;
        }

        public async Task<BlockImporterResult> ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<WithdrawalEntry> withdrawals,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct)
                .ConfigureAwait(false);
            sw.Stop();

            long blockNumber = (long)header.BlockNumber;
            int txCount = transactions?.Count ?? 0;
            Interlocked.Increment(ref _blocksTotal);
            Interlocked.Increment(ref _blocksSinceLastReport);

            if (!result.RootMatches)
            {
                Interlocked.Increment(ref _mismatchesTotal);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    string computedHex = result.ComputedStateRoot != null
                        ? result.ComputedStateRoot.ToHex()
                        : "<null>";
                    string expectedHex = result.ExpectedStateRoot != null
                        ? result.ExpectedStateRoot.ToHex()
                        : "<null>";
                    _logger.LogWarning(
                        "state-root mismatch: block={Block} computed=0x{Computed} expected=0x{Expected} txs={Txs} duration_ms={DurationMs}",
                        blockNumber,
                        computedHex.Length > 16 ? computedHex.Substring(0, 16) : computedHex,
                        expectedHex.Length > 16 ? expectedHex.Substring(0, 16) : expectedHex,
                        txCount,
                        sw.ElapsedMilliseconds);
                }
            }

            if (_blocksSinceLastReport >= _reportEvery)
            {
                long blocksThisReport = Interlocked.Exchange(ref _blocksSinceLastReport, 0);
                long elapsedMsTotal = _wall.ElapsedMilliseconds;
                long elapsedMsThisReport = elapsedMsTotal - Interlocked.Exchange(ref _lastReportElapsedMs, elapsedMsTotal);
                double recentBps = elapsedMsThisReport > 0 ? blocksThisReport * 1000.0 / elapsedMsThisReport : 0;
                double overallBps = elapsedMsTotal > 0 ? _blocksTotal * 1000.0 / elapsedMsTotal : 0;
                int peers = _pool?.ActivePeers?.Count ?? 0;
                Interlocked.Exchange(ref _lastReportBlockNumber, blockNumber);

                _logger.LogInformation(
                    "progress: block={Block} added={Added} total={Total} recent_bps={RecentBps:F1} avg_bps={AvgBps:F1} peers={Peers} mismatches={Mismatches} elapsed={Elapsed:hh\\:mm\\:ss}",
                    blockNumber, blocksThisReport, (int)_blocksTotal,
                    recentBps, overallBps, peers, _mismatchesTotal,
                    TimeSpan.FromMilliseconds(elapsedMsTotal));
            }

            return result;
        }
    }
}
