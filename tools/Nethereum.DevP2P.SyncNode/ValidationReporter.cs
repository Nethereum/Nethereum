using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    /// IBlockExecutor decorator that emits a structured milestone table every
    /// <c>milestoneEvery</c> blocks (default 25,000) and a one-line divergence
    /// callout every time the wrapped executor reports a non-matching state
    /// root. Supplements <see cref="ProgressReportingBlockExecutor"/> — both
    /// run side-by-side and serve different operators (one writes per-batch
    /// progress, this writes long-run sweep checkpoints + status file for the
    /// external SyncMonitor process to consume).
    /// </summary>
    internal sealed class ValidationReporter : IBlockExecutor
    {
        private readonly IBlockExecutor _inner;
        private readonly IPeerPool _pool;
        private readonly long _milestoneEvery;
        private readonly string _statusFile;
        private readonly ILogger _logger;
        private readonly Stopwatch _wall;
        private long _blocksTotal;
        private long _divergencesTotal;
        private long _lastMilestoneBlock;
        private long _lastDivergenceBlock;
        private string _lastDivergenceDetail;

        public ValidationReporter(
            IBlockExecutor inner,
            IPeerPool pool,
            long milestoneEvery,
            string statusFilePath,
            ILogger logger = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _pool = pool;
            _milestoneEvery = milestoneEvery <= 0 ? 25_000 : milestoneEvery;
            _statusFile = statusFilePath;
            _logger = logger ?? NullLogger.Instance;
            _wall = Stopwatch.StartNew();
        }

        public async Task<BlockImporterResult> ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<WithdrawalEntry> withdrawals,
            CancellationToken ct)
        {
            var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct)
                .ConfigureAwait(false);

            long blockNumber = (long)header.BlockNumber;
            Interlocked.Increment(ref _blocksTotal);

            if (!result.RootMatches)
            {
                long divCount = Interlocked.Increment(ref _divergencesTotal);
                Interlocked.Exchange(ref _lastDivergenceBlock, blockNumber);
                string computedHex = result.ComputedStateRoot != null ? result.ComputedStateRoot.ToHex() : "<null>";
                string expectedHex = result.ExpectedStateRoot != null ? result.ExpectedStateRoot.ToHex() : "<null>";
                string detail = $"computed=0x{Short(computedHex)} expected=0x{Short(expectedHex)}";
                Interlocked.Exchange(ref _lastDivergenceDetail, detail);

                string classification = ClassifyDivergence(result);

                Console.WriteLine(
                    $"[divergence block={blockNumber:N0}] classification={classification}  {detail}  total_divergences={divCount}");

                WriteStatusFile(blockNumber);
            }

            if (blockNumber - Interlocked.Read(ref _lastMilestoneBlock) >= _milestoneEvery)
            {
                Interlocked.Exchange(ref _lastMilestoneBlock, blockNumber);
                EmitMilestone(blockNumber);
            }

            return result;
        }

        private void EmitMilestone(long blockNumber)
        {
            int peers = _pool?.ActivePeers?.Count ?? 0;
            long total = Interlocked.Read(ref _blocksTotal);
            long divergences = Interlocked.Read(ref _divergencesTotal);
            double bps = total / Math.Max(0.001, _wall.Elapsed.TotalSeconds);
            string elapsed = _wall.Elapsed.ToString(@"hh\:mm\:ss");
            string canary = divergences == 0 ? "PASS" : "FAIL";

            Console.WriteLine(
                $"[report block={blockNumber:N0}] peers={peers}  divergences={divergences}  blocks/sec={bps:F0}  elapsed={elapsed}  state-root canary={canary}");

            WriteStatusFile(blockNumber);
        }

        private void WriteStatusFile(long blockNumber)
        {
            if (string.IsNullOrEmpty(_statusFile)) return;
            try
            {
                var status = new Dictionary<string, object>
                {
                    ["lastBlock"] = blockNumber,
                    ["blocksExecuted"] = Interlocked.Read(ref _blocksTotal),
                    ["divergences"] = Interlocked.Read(ref _divergencesTotal),
                    ["lastDivergenceBlock"] = Interlocked.Read(ref _lastDivergenceBlock),
                    ["lastDivergenceDetail"] = Volatile.Read(ref _lastDivergenceDetail) ?? "",
                    ["peers"] = _pool?.ActivePeers?.Count ?? 0,
                    ["elapsedSeconds"] = _wall.Elapsed.TotalSeconds,
                    ["blocksPerSec"] = Interlocked.Read(ref _blocksTotal) / Math.Max(0.001, _wall.Elapsed.TotalSeconds),
                    ["timestampUtc"] = DateTime.UtcNow.ToString("o")
                };
                var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_statusFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("status file write failed: {Error}", ex.Message);
            }
        }

        private static string ClassifyDivergence(BlockImporterResult result)
        {
            if (result.ExpectedStateRoot == null) return "SourceUnavailable";
            if (result.ComputedStateRoot == null) return "EvmBug";
            return "EvmBug";
        }

        private static string Short(string hex)
            => hex == null ? "<null>" : (hex.Length > 16 ? hex.Substring(0, 16) : hex);
    }
}
