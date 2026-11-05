using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Signer;

namespace Nethereum.MainnetChain.Replay
{
    public static class MainnetReplayRunner
    {
        public static async Task<int> RunAsync(
            MainnetChainServerConfig config, ILoggerFactory loggerFactory, CancellationToken ct = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            if (string.IsNullOrWhiteSpace(config.DataDir))
                throw new ArgumentException("DataDir is required to replay blocks.", nameof(config));
            if (config.ReplayBlocks == 0)
                throw new ArgumentException("ReplayBlocks must be greater than zero.", nameof(config));

            var logger = loggerFactory.CreateLogger("Nethereum.MainnetChain.Replay");
            var report = new ReplayReport { RequestedBlocks = config.ReplayBlocks };
            var started = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await ReplayAsync(config, loggerFactory, logger, report, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "replay: failed before or during execution at {DataDir}", config.DataDir);
                report.Succeeded = false;
                report.Outcome = "Failed";
                report.FailureDetail ??= $"{ex.GetType().Name}: {ex.Message}";
            }

            report.ElapsedSeconds = started.Elapsed.TotalSeconds;
            Write(config.ReplayReport, report, logger);
            return report.Succeeded ? 0 : 1;
        }

        private static async Task ReplayAsync(
            MainnetChainServerConfig config,
            ILoggerFactory loggerFactory,
            ILogger logger,
            ReplayReport report,
            CancellationToken ct)
        {
            var nodeConfig = config.ToChainNodeConfig();
            await using var storage = await MainnetNodeComposition.OpenAndRecoverAsync(
                config, nodeConfig, new BootRecoveryGate(logger), logger).ConfigureAwait(false);
            var bundle = storage.Bundle;

            var bounds = EffectiveStartBlockResolver.Resolve(
                bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);
            var head = bounds.StartBlock - 1;
            var end = head + config.ReplayBlocks;
            report.ExecutedHead = head;

            var missingHeader = await FirstBlockWithoutHeaderAsync(bundle, head + 1, end).ConfigureAwait(false);
            if (missingHeader is ulong missing)
            {
                report.Outcome = "Failed";
                report.FailedBlock = missing;
                report.FailureDetail = $"no stored header for block {missing}; the replay never fetches from the network";
                logger.LogError("replay: {Detail}", report.FailureDetail);
                return;
            }

            var tip = await bundle.Blocks.GetByNumberAsync(new BigInteger(end)).ConfigureAwait(false);
            var tipHash = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(end)).ConfigureAwait(false);

            var cadence = MainnetNodeComposition.BuildFlushCadence(config);
            report.FlushCadenceBlocks = (cadence as FixedIntervalFlushCadence)?.K ?? 1UL;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var manager = storage.Manager;
            Func<RocksDbCacheStats>? cacheProbe = manager is { StatisticsEnabled: true } ? manager.ReadCacheStats : null;
            using var recorder = new ReplayRecorder(
                new AlwaysAcceptConsensusBlockGate(),
                cadence ?? new EveryBlockFlushCadence(),
                head,
                cacheProbe,
                onRejected: (block, reason) =>
                {
                    logger.LogError("replay.mismatch block={Block} {Reason}", block, reason);
                    cts.Cancel();
                });
            loggerFactory.AddProvider(recorder);

            var options = EffectiveStartBlockResolver.BuildOptions(
                bounds with { EndBlock = end }, config) with { ExternalHeaderFollow = true };
            var node = new MainnetChainNodeFactory(recorder, loggerFactory, null, cadence, config.PrefetchWorkers, config.StateRootWorkers).Build(
                bundle,
                new LocalReplayBlockSource(new List<BlockBundle>()),
                MainnetNodeComposition.BuildValidationPolicy(config, loggerFactory.CreateLogger<StrictValidationPolicy>()),
                options,
                new FixedTipCanonicalSource(end, tipHash, tip.StateRoot));

            logger.LogInformation(
                "replay: executing blocks {From}..{To} from {DataDir} (flush_cadence={Cadence})",
                head + 1, end, config.DataDir, report.FlushCadenceBlocks);
            recorder.SnapshotCache("start", head);
            FollowerRunResult result;
            try
            {
                result = await node.RunAsync(options, cts.Token, logger).ConfigureAwait(false);
            }
            finally
            {
                node.ReleaseExecutionResources();
            }
            recorder.SnapshotCache("end", head + (ulong)recorder.Blocks.Count);

            Fill(report, recorder, result, config.ReplayBlocks);
            LogSummary(logger, report);
        }

        private static async Task<ulong?> FirstBlockWithoutHeaderAsync(IChainStoreBundle bundle, ulong from, ulong to)
        {
            for (var block = from; block <= to; block++)
                if (await bundle.Blocks.GetHashByNumberAsync(new BigInteger(block)).ConfigureAwait(false) == null)
                    return block;
            return null;
        }

        private static void Fill(ReplayReport report, ReplayRecorder recorder, FollowerRunResult result, ulong requested)
        {
            report.BlocksExecuted = (ulong)recorder.Blocks.Count;
            report.Blocks = recorder.Blocks.ToList();
            report.Summary = ReplaySummaryCalculator.Summarise(report.Blocks);
            report.Cache = BuildCacheReport(recorder.CacheSnapshots);
            report.Diagnostics = recorder.Diagnostics.ToList();

            var rejected = recorder.RejectedBlock;
            report.Succeeded = rejected == null
                && result.ExitReason == FollowerExitReason.SourceCompleted
                && result.BlocksExecuted == requested
                && report.Blocks.Count == (int)requested;
            report.Outcome = report.Succeeded ? "AllRootsMatched" : rejected != null ? "RootMismatch" : "Failed";
            if (report.Succeeded) return;

            report.FailedBlock = rejected ?? result.LastExecutedBlock + 1;
            report.FailureDetail = recorder.RejectionReason ?? $"{result.ExitReason}: {result.Detail}";
        }

        private static ReplayCacheReport? BuildCacheReport(IReadOnlyList<ReplayCacheSnapshot> snapshots)
        {
            if (snapshots.Count == 0) return null;
            var cache = new ReplayCacheReport { Snapshots = snapshots.ToList() };
            for (var i = 1; i < snapshots.Count; i++)
                if (snapshots[i].Label == "window")
                    cache.WindowDeltas.Add(Delta(snapshots[i - 1], snapshots[i], "window"));
            cache.Total = Delta(snapshots[0], snapshots[snapshots.Count - 1], "total");
            return cache;
        }

        private static ReplayCacheSnapshot Delta(ReplayCacheSnapshot from, ReplayCacheSnapshot to, string label)
            => new ReplayCacheSnapshot
            {
                Label = label,
                AtBlock = to.AtBlock,
                IndexHit = to.IndexHit - from.IndexHit,
                IndexMiss = to.IndexMiss - from.IndexMiss,
                FilterHit = to.FilterHit - from.FilterHit,
                FilterMiss = to.FilterMiss - from.FilterMiss,
                DataHit = to.DataHit - from.DataHit,
                DataMiss = to.DataMiss - from.DataMiss,
                UsageBytes = to.UsageBytes,
            };

        private static void LogSummary(ILogger logger, ReplayReport report)
        {
            var summary = report.Summary!;
            logger.LogInformation(
                "replay.summary outcome={Outcome} blocks={Blocks}/{Requested} wall_ms_median={WallMedian:F2} wall_ms_p90={WallP90:F2} " +
                "exec_ms_median={ExecMedian:F1} root_ms_median={RootMedian:F1} persist_ms_median={PersistMedian:F1} " +
                "execution_ms_median={ExecutionMedian:F2} state_read_ms_median={StateReadMedian:F2} state_hash_ms_median={StateHashMedian:F2} commit_ms_median={CommitMedian:F2} " +
                "alloc_bytes_median={Alloc:F0} rss_slope_bytes_per_block={RssSlope:F0} mgas_per_s={Mgas:F2}",
                report.Outcome, report.BlocksExecuted, report.RequestedBlocks,
                summary.WallMs.Median, summary.WallMs.P90,
                summary.ExecMs.Median, summary.RootMs.Median, summary.PersistMs.Median,
                summary.ExecutionMs.Median, summary.StateReadMs.Median, summary.StateHashMs.Median, summary.CommitMs.Median,
                summary.MedianAllocBytesPerBlock, summary.Rss.SlopeBytesPerBlock, summary.MegagasPerSecond);
            if (!report.Succeeded)
                logger.LogError("replay.failed block={Block} {Detail}", report.FailedBlock, report.FailureDetail);
        }

        private static void Write(string? path, ReplayReport report, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(report, ReplayReportJsonContext.Default.ReplayReport));
            logger.LogInformation("replay: report written to {Path}", path);
        }
    }
}
