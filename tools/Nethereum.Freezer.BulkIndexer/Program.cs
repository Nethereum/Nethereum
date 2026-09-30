using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Freezer;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;

namespace Nethereum.Freezer.BulkIndexer
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var opts = ToolOptions.Parse(args);
            if (opts == null)
            {
                Console.Error.WriteLine(
                    "usage: nethereum-freezer-bulkindexer --freezer <ancient-dir> --index <rocksdb-dir> " +
                    "[--dop N] [--run-entries N] [--run-bytes N] [--resident-bytes N] [--sort-dop N] [--to BLOCK] " +
                    "[--block-cache-mb N] [--log-throttle-seconds N]\n" +
                    "  --dop N            degree of parallelism (default: max(2, CPU count - 2))\n" +
                    "  --run-entries N    entry-count flush trigger (default: int.MaxValue; bytes governs)\n" +
                    "  --run-bytes N      byte-size flush trigger (default: 2 GiB)\n" +
                    "  --resident-bytes N in-memory pending-run ceiling (default: 6 GiB)\n" +
                    "  --sort-dop N       run-sort degree of parallelism (default: 0 => CPU count)");
                return 1;
            }
            Console.WriteLine(opts.Describe());

            if (!Directory.Exists(opts.FreezerDir))
            {
                Console.Error.WriteLine($"freezer directory not found: {opts.FreezerDir}");
                return 1;
            }
            Directory.CreateDirectory(opts.IndexDir);

            var stepGate = Stopwatch.StartNew();
            var stepLock = new object();
            var logThrottle = TimeSpan.FromSeconds(opts.LogThrottleSeconds);
            FreezerParallelSegment.StepLog = s =>
            {
                if (logThrottle > TimeSpan.Zero)
                {
                    lock (stepLock) { if (stepGate.Elapsed < logThrottle) return; stepGate.Restart(); }
                }
                Console.Error.WriteLine("[step] " + s);
            };

            using var freezer = Nethereum.Freezer.Freezer.Open(
                new FreezerLayout(opts.FreezerDir), FreezerOpenMode.ReadOnly);
            var codecs = new FreezerCodecSet();

            using var indexRocks = new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = opts.IndexDir, BlockCacheSize = opts.BlockCacheBytes },
                CatalogueScope.FreezerHistory);

            var progress = new FreezerHistoryIndexProgress(indexRocks, HistoryColumnFamilies.Control);
            var indexLock = new object();
            var ingestor = new BulkIndexIngestor(
                indexRocks.Database,
                Path.Combine(opts.IndexDir, "bulk-scratch"),
                new[] { HistoryColumnFamilies.BlockHashIndex, HistoryColumnFamilies.TxHashIndex },
                windowHead => { lock (indexLock) progress.SetByHashCursor(windowHead + 1); },
                checkpointIntervalBlocks: int.MaxValue,
                checkpointIntervalSeconds: 0,
                maxPendingEntries: opts.RunEntries,
                stepLog: s => Console.Error.WriteLine("[run] " + s),
                maxPendingBytes: opts.RunBytes,
                maxResidentBytes: opts.ResidentBytes,
                forceCheckpointOnFirstAccumulate: false,
                sortDegreeOfParallelism: opts.SortDop);

            var fmStore = new RocksDbFilterMapsStore(indexRocks);
            var chainView = new SegmentChainView(freezer, codecs, maxDegreeOfParallelism: opts.ResolvedDop);
            var finality = new FreezerHeadFinalitySource(freezer);
            var fmIndexer = new FilterMapsIndexer(
                fmStore, chainView, finality, Nethereum.Freezer.FilterMaps.FilterMapsParams.Default,
                renderDegreeOfParallelism: opts.ResolvedDop);

            var builder = new FreezerBulkIndexBuilder(
                freezer, codecs, ingestor, fmStore, fmIndexer, progress,
                byHashDegreeOfParallelism: opts.ResolvedDop,
                logDegreeOfParallelism: opts.ResolvedDop);
            var head = opts.ToBlock ?? builder.ComputeIndexableBoundary();
            Console.WriteLine($"freezer_items={freezer.Items} target_head={head}");

            var sw = Stopwatch.StartNew();
            Console.WriteLine("[concurrent] by-hash index (block-hash + tx-hash) + filter-maps log render...");
            var epochs = 0;
            var byHashTask = Task.Factory.StartNew(
                () => builder.CatchUpByHash(head, CancellationToken.None),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var renderTask = Task.Factory.StartNew(
                () => epochs = builder.RenderFilterMaps(CancellationToken.None, head),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task.WaitAll(byHashTask, renderTask);
            Console.WriteLine($"      by-hash indexed to {builder.ByHashIndexedHead} ({sw.Elapsed})");
            Console.WriteLine($"      rendered {epochs} epoch(s), log head {builder.LogIndexRenderedHead} ({sw.Elapsed})");

            ingestor.Finish();
            fmStore.FlushIfDirty();
            sw.Stop();
            Console.WriteLine($"DONE by_hash={builder.ByHashIndexedHead} log_rendered={builder.LogIndexRenderedHead} wall={sw.Elapsed}");
            return 0;
        }
    }

    internal sealed class ToolOptions
    {
        public string FreezerDir;
        public string IndexDir;
        public int? Dop;
        public int RunEntries = int.MaxValue;
        public long RunBytes = 2L * 1024 * 1024 * 1024;
        public long ResidentBytes = BulkIndexIngestor.DefaultResidentCeilingBytes;
        public int SortDop = 0;
        public long? ToBlock;
        public long BlockCacheBytes = 512L * 1024 * 1024;
        public int LogThrottleSeconds = 0;

        public int ResolvedDop => Dop ?? Math.Max(2, Environment.ProcessorCount - 2);

        public static ToolOptions Parse(string[] args)
        {
            if (args.Length % 2 != 0) return null;
            var o = new ToolOptions();
            for (var i = 0; i < args.Length; i += 2)
            {
                var v = args[i + 1];
                switch (args[i])
                {
                    case "--freezer": o.FreezerDir = v; break;
                    case "--index": o.IndexDir = v; break;
                    case "--dop": o.Dop = int.Parse(v); break;
                    case "--run-entries": o.RunEntries = int.Parse(v); break;
                    case "--run-bytes": o.RunBytes = long.Parse(v); break;
                    case "--resident-bytes": o.ResidentBytes = long.Parse(v); break;
                    case "--sort-dop": o.SortDop = int.Parse(v); break;
                    case "--to": o.ToBlock = long.Parse(v); break;
                    case "--block-cache-mb": o.BlockCacheBytes = long.Parse(v) * 1024 * 1024; break;
                    case "--log-throttle-seconds": o.LogThrottleSeconds = int.Parse(v); break;
                    default: return null;
                }
            }
            return o.FreezerDir == null || o.IndexDir == null ? null : o;
        }

        public string Describe()
            => $"freezer-bulkindexer freezer={FreezerDir} index={IndexDir} dop={ResolvedDop} " +
               $"runEntries={RunEntries:N0} runBytes={RunBytes:N0} residentBytes={ResidentBytes:N0} " +
               $"sortDop={(SortDop == 0 ? "cpuCount" : SortDop.ToString())} " +
               $"to={(ToBlock?.ToString() ?? "head")} logThrottleSeconds={LogThrottleSeconds}";
    }
}
