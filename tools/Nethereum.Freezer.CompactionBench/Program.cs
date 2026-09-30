using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using RocksDbSharp;

namespace Nethereum.Freezer.CompactionBench
{
    internal static class Program
    {
        private const int KeyBytes = 32;
        private const int ValueBytes = 12;

        // Approximates a real Merkle-Patricia trie node's RLP-encoded size (branch/extension/leaf
        // nodes are typically a few dozen to a few hundred bytes; this sits in that range).
        private const int TrieNodeValueBytes = 110;

        private static int Main(string[] args)
        {
            var opts = BenchOptions.Parse(args);
            Console.WriteLine(opts.Describe());

            if (opts.Mode == "point") return RunPointMode(opts);
            return RunSstMode(opts);
        }

        // Sorted-SST-ingest mode: the flat-state / freezer-index bulk-load pattern (30ece6a2c).
        // Kept as the original behavior for the freezer-index benchmark this tool was built for.
        private static int RunSstMode(BenchOptions opts)
        {
            if (Directory.Exists(opts.DbDir)) Directory.Delete(opts.DbDir, true);
            Directory.CreateDirectory(opts.DbDir);
            Directory.CreateDirectory(opts.ScratchDir);

            var cache = Cache.CreateLru(256UL * 1024 * 1024);
            var cfOptions = opts.Style == "leveled"
                ? RocksProfiles.LiveIndex(cache)
                : RocksProfiles.UniversalIndexCf(cache, opts.Universal);

            var dbOptions = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true)
                .SetMaxBackgroundCompactions(opts.BackgroundCompactions)
                .SetMaxBackgroundFlushes(2);

            var families = new ColumnFamilies
            {
                { "default", new ColumnFamilyOptions() },
                { "bench", cfOptions },
            };

            using var db = RocksDb.Open(dbOptions, opts.DbDir, families);
            var cf = db.GetColumnFamily("bench");

            var optionsText = ReadOptionsFile(opts.DbDir);
            var effectiveStyle = ExtractLines(optionsText, "compaction_style");
            Console.WriteLine($"effective compaction_style (from OPTIONS): {effectiveStyle}");
            if (opts.Style == "universal")
            {
                if (!effectiveStyle.Contains("Universal"))
                {
                    Console.WriteLine("FAIL: universal compaction was NOT applied (OPTIONS shows no Universal style). Measurement would be meaningless.");
                    return 2;
                }
                var missing = MissingUniversalSubOptions(optionsText, opts.Universal);
                if (missing.Count > 0)
                {
                    Console.WriteLine($"FAIL: universal sub-options did not take effect (OPTIONS missing: {string.Join(", ", missing)}). The CF silently fell back to defaults; measurement would be misleading.");
                    return 2;
                }
                Console.WriteLine("verified universal sub-options in OPTIONS match requested settings");
            }

            var rng = new Random(12345);
            long rawBytes = 0;
            var totalSw = Stopwatch.StartNew();

            for (var run = 1; run <= opts.Runs; run++)
            {
                var sw = Stopwatch.StartNew();
                var path = Path.Combine(opts.ScratchDir, $"run_{run}.sst");
                var written = WriteSortedRun(path, opts.EntriesPerRun, rng);
                rawBytes += written;
                db.IngestExternalFiles(new[] { path }, new IngestExternalFileOptions(), cf);
                try { File.Delete(path); } catch { }
                sw.Stop();

                var files = SstSizes(opts.DbDir);
                Console.WriteLine($"run {run,3}/{opts.Runs}  ingest+gen {sw.ElapsedMilliseconds,6}ms  sst_files={files.Count,4}  {SizeClasses(files)}  pending={Pending(db, cf)}");
            }

            Console.WriteLine("all runs ingested; waiting for compaction to settle...");
            var settled = WaitForCompaction(db, cf, opts.SettleTimeout);
            totalSw.Stop();

            var finalFiles = SstSizes(opts.DbDir);
            Console.WriteLine();
            Console.WriteLine(settled
                ? "=== SETTLED (compaction converged, pending=0) ==="
                : $"=== NOT SETTLED — timed out after {opts.SettleTimeout}, pending={Gb(Pending(db, cf)):F2} GB; numbers below are a mid-compaction snapshot, NOT steady state ===");
            Console.WriteLine($"wall={totalSw.Elapsed}  raw_ingested={Gb(rawBytes):F2} GB  sst_files={finalFiles.Count}");
            Console.WriteLine($"size classes: {SizeClasses(finalFiles)}");
            Console.WriteLine($"levelstats:\n{db.GetProperty("rocksdb.levelstats", cf)}");
            var (cw, cr) = CumulativeCompactionGb(db, cf);
            Console.WriteLine($"cumulative compaction: write={cw:F2} GB  read={cr:F2} GB");
            if (rawBytes > 0)
                Console.WriteLine($"WRITE AMPLIFICATION (compaction write / raw ingested) = {(cw / Gb(rawBytes)):F2}x{(settled ? "" : "  [UNSETTLED — floor only]")}");
            Console.WriteLine();
            Console.WriteLine("verdict: TIER if size classes cluster into a few comparable bands (2G x4 -> 8G);");
            Console.WriteLine("         DEGENERATE if one large file dominates and many small runs surround it.");
            return 0;
        }

        // Point-write mode: matches RocksDbPathTrieNodeStore.Commit's actual mechanism -- a fresh
        // WriteBatch per call, db.Write(batch), no sorting, no SST ingestion. Multiple concurrent
        // workers each write scattered random keys (mirroring Phase-2's work-stealing consumers,
        // whose per-writer arrival order is NOT sorted -- 30ece6a2c) so the comparison exercises
        // the actual defect class under investigation, not the already-benchmarked SST-ingest path.
        private static int RunPointMode(BenchOptions opts)
        {
            if (Directory.Exists(opts.DbDir)) Directory.Delete(opts.DbDir, true);
            Directory.CreateDirectory(opts.DbDir);

            var cache = Cache.CreateLru(256UL * 1024 * 1024);
            var cfOptions = opts.Style == "leveled"
                ? RocksProfiles.ForLive(LiveCfProfile.RandomPoint, cache, StoragePreset.MainnetFull)
                : RocksProfiles.ForLive(LiveCfProfile.UniversalBulkPoint, cache, StoragePreset.MainnetFull);

            // Mirrors RocksDbManager.CreateDbOptions's actual production values (RocksDbOptions.cs
            // defaults), not the bench's own ad-hoc numbers -- an independent review found the
            // original bench under-provisioned subcompactions (1 implicit vs prod's 4), which
            // specifically handicaps the Leveled arm's L0->L1 compaction throughput.
            var dbOptions = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true)
                .SetMaxBackgroundCompactions(8)
                .SetMaxBackgroundFlushes(2)
                .SetMaxTotalWalSize(512UL * 1024 * 1024)
                .SetBytesPerSync(1UL * 1024 * 1024)
                .SetDbWriteBufferSize(3UL * 1024 * 1024 * 1024)
                .SetMaxOpenFiles(10000);
            Native.Instance.rocksdb_options_set_max_subcompactions(dbOptions.Handle, 4);
            Native.Instance.rocksdb_options_set_max_background_jobs(dbOptions.Handle, 8);

            var families = new ColumnFamilies
            {
                { "default", new ColumnFamilyOptions() },
                { "bench", cfOptions },
            };

            using var db = RocksDb.Open(dbOptions, opts.DbDir, families);
            var cf = db.GetColumnFamily("bench");
            var sampleKeys = new ConcurrentBag<byte[]>();

            var optionsText = ReadOptionsFile(opts.DbDir);
            var effectiveStyle = ExtractLines(optionsText, "compaction_style");
            Console.WriteLine($"effective compaction_style (from OPTIONS): {effectiveStyle}");
            if (opts.Style == "universal" && !effectiveStyle.Contains("Universal"))
            {
                Console.WriteLine("FAIL: universal compaction was NOT applied. Measurement would be meaningless.");
                return 2;
            }
            if (opts.Style == "leveled" && effectiveStyle.Contains("Universal"))
            {
                Console.WriteLine("FAIL: expected leveled but OPTIONS shows Universal. Measurement would be meaningless.");
                return 2;
            }

            long totalEntries = (long)opts.Runs * opts.EntriesPerRun;
            long rawBytes = totalEntries * (KeyBytes + TrieNodeValueBytes);
            var totalSw = Stopwatch.StartNew();
            long written = 0;
            var progressLock = new object();

            Parallel.For(0, opts.Workers, workerIdx =>
            {
                var rng = new Random(12345 + workerIdx);
                var entriesPerWorker = totalEntries / opts.Workers;
                const int batchSize = 5_000;
                var value = new byte[TrieNodeValueBytes];
                long doneInWorker = 0;
                while (doneInWorker < entriesPerWorker)
                {
                    using var batch = new WriteBatch();
                    var thisBatch = (int)Math.Min(batchSize, entriesPerWorker - doneInWorker);
                    for (var i = 0; i < thisBatch; i++)
                    {
                        var k = new byte[KeyBytes];
                        rng.NextBytes(k); // scattered random keys -- NOT sorted, NOT deduped: the real defect class
                        rng.NextBytes(value);
                        batch.Put(k, value, cf);
                        if (i % 500 == 0) sampleKeys.Add(k); // retain a sample for a real point-read measurement
                    }
                    db.Write(batch);
                    doneInWorker += thisBatch;
                    var totalDone = Interlocked.Add(ref written, thisBatch);
                    if (totalDone % (entriesPerWorker * opts.Workers / 20) < batchSize)
                    {
                        lock (progressLock)
                        {
                            Console.WriteLine($"  {totalDone,12:N0}/{totalEntries:N0} written  pending={Gb(Pending(db, cf)):F2}GB  elapsed={totalSw.Elapsed}");
                        }
                    }
                }
            });

            Console.WriteLine($"all {totalEntries:N0} point writes issued by {opts.Workers} concurrent workers; waiting for compaction to settle...");
            var settled = WaitForCompaction(db, cf, opts.SettleTimeout);
            totalSw.Stop();

            var finalFiles = SstSizes(opts.DbDir);
            Console.WriteLine();
            Console.WriteLine(settled
                ? "=== SETTLED (compaction converged, pending=0) ==="
                : $"=== NOT SETTLED — timed out after {opts.SettleTimeout}, pending={Gb(Pending(db, cf)):F2} GB; numbers below are a mid-compaction snapshot, NOT steady state ===");
            Console.WriteLine($"wall={totalSw.Elapsed}  raw_written={Gb(rawBytes):F2} GB  sst_files={finalFiles.Count}");
            Console.WriteLine($"size classes: {SizeClasses(finalFiles)}");
            var (cw, cr) = CumulativeCompactionGb(db, cf);
            Console.WriteLine($"cumulative compaction: write={cw:F2} GB  read={cr:F2} GB");
            Console.WriteLine($"WRITE AMPLIFICATION (compaction write / raw written) = {(cw / Gb(rawBytes)):F2}x{(settled ? "" : "  [UNSETTLED — floor only]")}");

            // Point-read measurement at steady state -- the write-amp numbers alone say nothing
            // about the read/space-amp tradeoff Universal is documented to make; measure it.
            MeasurePointReads(db, cf, sampleKeys);
            return 0;
        }

        private static void MeasurePointReads(RocksDb db, ColumnFamilyHandle cf, ConcurrentBag<byte[]> sampleKeys)
        {
            var hitKeys = sampleKeys.ToArray();
            if (hitKeys.Length == 0)
            {
                Console.WriteLine("point-read: no sample keys retained, skipping");
                return;
            }
            var rng = new Random(999);
            var hitSample = new byte[Math.Min(20_000, hitKeys.Length)][];
            for (var i = 0; i < hitSample.Length; i++) hitSample[i] = hitKeys[rng.Next(hitKeys.Length)];

            var missSample = new byte[10_000][];
            for (var i = 0; i < missSample.Length; i++)
            {
                var k = new byte[KeyBytes];
                rng.NextBytes(k);
                missSample[i] = k;
            }

            long hitFound = 0;
            var swHit = Stopwatch.StartNew();
            foreach (var k in hitSample)
                if (db.Get(k, cf) != null) hitFound++;
            swHit.Stop();

            var swMiss = Stopwatch.StartNew();
            foreach (var k in missSample)
                db.Get(k, cf);
            swMiss.Stop();

            Console.WriteLine();
            Console.WriteLine("=== POINT-READ MEASUREMENT (steady state, after settle) ===");
            Console.WriteLine($"hits:  {hitSample.Length:N0} reads, {hitFound:N0} found ({(100.0 * hitFound / hitSample.Length):F1}%), " +
                               $"total={swHit.Elapsed}, avg={(swHit.Elapsed.TotalMilliseconds * 1000.0 / hitSample.Length):F1}us/read");
            Console.WriteLine($"misses:{missSample.Length:N0} reads, total={swMiss.Elapsed}, avg={(swMiss.Elapsed.TotalMilliseconds * 1000.0 / missSample.Length):F1}us/read (bloom-filter-rejected, no data block touched)");
        }

        private static long WriteSortedRun(string path, int entries, Random rng)
        {
            var keys = new List<byte[]>(entries);
            for (var i = 0; i < entries; i++)
            {
                var k = new byte[KeyBytes];
                rng.NextBytes(k);
                keys.Add(k);
            }
            keys.Sort(CompareBytes);

            var value = new byte[ValueBytes];
            long raw = 0;
            using var writer = new SstFileWriter(new EnvOptions(), RocksProfiles.IndexSstOptions());
            writer.Open(path);
            byte[] prev = null;
            foreach (var k in keys)
            {
                if (prev != null && CompareBytes(prev, k) == 0) continue;
                rng.NextBytes(value);
                writer.Put(k, value);
                prev = k;
                raw += KeyBytes + ValueBytes;
            }
            writer.Finish();
            return raw;
        }

        private static bool WaitForCompaction(RocksDb db, ColumnFamilyHandle cf, TimeSpan timeout)
        {
            // rocksdb.estimate-pending-compaction-bytes is hard-coded to 0 for Universal (only
            // implemented for Leveled -- confirmed against RocksDB's own source), so under
            // Universal this degrades to polling num-running-compactions alone. A single clean
            // 1s sample can land in the gap between two scheduled jobs; require several
            // consecutive clean samples before declaring settled to close that race.
            const int requiredConsecutiveCleanSamples = 5;
            var sw = Stopwatch.StartNew();
            var consecutiveClean = 0;
            while (sw.Elapsed < timeout)
            {
                var pending = Pending(db, cf);
                var running = db.GetProperty("rocksdb.num-running-compactions", cf);
                var clean = pending == 0 && (running == "0" || string.IsNullOrEmpty(running));
                consecutiveClean = clean ? consecutiveClean + 1 : 0;
                if (consecutiveClean >= requiredConsecutiveCleanSamples) return true;
                Thread.Sleep(1000);
            }
            return false;
        }

        private static long Pending(RocksDb db, ColumnFamilyHandle cf)
            => long.TryParse(db.GetProperty("rocksdb.estimate-pending-compaction-bytes", cf), out var v) ? v : -1;

        private static List<long> SstSizes(string dbDir)
            => new DirectoryInfo(dbDir).GetFiles("*.sst").Select(f => f.Length).OrderByDescending(s => s).ToList();

        private static string SizeClasses(List<long> files)
        {
            if (files.Count == 0) return "(none)";
            var buckets = new SortedDictionary<int, int>();
            foreach (var size in files)
            {
                var mb = (int)Math.Round(size / (1024.0 * 1024.0));
                var bucket = mb <= 0 ? 0 : (int)Math.Pow(2, Math.Round(Math.Log2(Math.Max(1, mb))));
                if (!buckets.ContainsKey(bucket)) buckets[bucket] = 0;
                buckets[bucket]++;
            }
            return string.Join(" ", buckets.Select(b => $"~{b.Key}MB x{b.Value}"));
        }

        private static (double Write, double Read) CumulativeCompactionGb(RocksDb db, ColumnFamilyHandle cf)
        {
            var stats = db.GetProperty("rocksdb.stats", cf) ?? string.Empty;
            foreach (var line in stats.Split('\n'))
            {
                var t = line.Trim();
                if (!t.StartsWith("Cumulative compaction:", StringComparison.Ordinal)) continue;
                double w = 0, r = 0;
                var tokens = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.TrimEnd(',')).ToArray();
                for (var i = 0; i < tokens.Length - 2; i++)
                {
                    if (tokens[i + 1] == "GB" && tokens[i + 2] == "write" && double.TryParse(tokens[i], out var wv)) w = wv;
                    if (tokens[i + 1] == "GB" && tokens[i + 2] == "read" && double.TryParse(tokens[i], out var rv)) r = rv;
                }
                return (w, r);
            }
            return (0, 0);
        }

        private static string ReadOptionsFile(string dbDir)
        {
            var options = new DirectoryInfo(dbDir).GetFiles("OPTIONS-*").OrderByDescending(f => f.Name).FirstOrDefault();
            return options == null ? string.Empty : File.ReadAllText(options.FullName);
        }

        private static string ExtractLines(string optionsText, string keyPrefix)
        {
            var lines = optionsText.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith(keyPrefix, StringComparison.Ordinal))
                .Distinct()
                .ToArray();
            return lines.Length == 0 ? "(not found)" : string.Join(" | ", lines);
        }

        private static List<string> MissingUniversalSubOptions(string optionsText, UniversalCompactionSettings s)
        {
            var expected = new[]
            {
                $"size_ratio={s.SizeRatioPercent}",
                $"min_merge_width={s.MinMergeWidth}",
                $"max_merge_width={s.MaxMergeWidth}",
                $"max_size_amplification_percent={s.MaxSizeAmplificationPercent}",
                s.StopStyleSimilarSize ? "stop_style=kCompactionStopStyleSimilarSize" : "stop_style=kCompactionStopStyleTotalSize",
            };
            return expected.Where(e => !optionsText.Contains(e, StringComparison.Ordinal)).ToList();
        }

        private static double Gb(long bytes) => bytes / (1024.0 * 1024.0 * 1024.0);

        private static int CompareBytes(byte[] a, byte[] b)
        {
            var len = Math.Min(a.Length, b.Length);
            for (var i = 0; i < len; i++) { var d = a[i].CompareTo(b[i]); if (d != 0) return d; }
            return a.Length.CompareTo(b.Length);
        }
    }

    internal sealed class BenchOptions
    {
        public int Runs = 16;
        public int EntriesPerRun = 6_000_000;
        public string Style = "universal";
        public int BackgroundCompactions = 4;
        public string DbDir = Path.Combine(Path.GetTempPath(), "neth-compbench", "db");
        public string ScratchDir = Path.Combine(Path.GetTempPath(), "neth-compbench", "scratch");
        public TimeSpan SettleTimeout = TimeSpan.FromMinutes(20);
        public UniversalCompactionSettings Universal = UniversalCompactionSettings.BulkDefault;
        public string Mode = "sst";
        public int Workers = 4;

        public static BenchOptions Parse(string[] args)
        {
            if (args.Length % 2 != 0)
                throw new ArgumentException($"odd number of arguments ({args.Length}); every --flag needs a value");

            var o = new BenchOptions();
            for (var i = 0; i < args.Length; i += 2)
            {
                var v = args[i + 1];
                switch (args[i])
                {
                    case "--runs": o.Runs = int.Parse(v); break;
                    case "--entries-per-run": o.EntriesPerRun = int.Parse(v); break;
                    case "--style": o.Style = v; break;
                    case "--background-compactions": o.BackgroundCompactions = int.Parse(v); break;
                    case "--dir": o.DbDir = Path.Combine(v, "db"); o.ScratchDir = Path.Combine(v, "scratch"); break;
                    case "--l0-trigger": o.Universal.Level0FileNumCompactionTrigger = int.Parse(v); break;
                    case "--min-merge-width": o.Universal.MinMergeWidth = int.Parse(v); break;
                    case "--max-merge-width": o.Universal.MaxMergeWidth = int.Parse(v); break;
                    case "--size-ratio": o.Universal.SizeRatioPercent = int.Parse(v); break;
                    case "--settle-minutes": o.SettleTimeout = TimeSpan.FromMinutes(double.Parse(v)); break;
                    case "--mode": o.Mode = v; break;
                    case "--workers": o.Workers = int.Parse(v); break;
                    default: throw new ArgumentException($"unknown argument '{args[i]}'");
                }
            }
            if (o.Style != "universal" && o.Style != "leveled")
                throw new ArgumentException($"--style must be 'universal' or 'leveled', got '{o.Style}'");
            if (o.Mode != "sst" && o.Mode != "point")
                throw new ArgumentException($"--mode must be 'sst' or 'point', got '{o.Mode}'");
            return o;
        }

        public string Describe()
            => $"CompactionBench style={Style} runs={Runs} entriesPerRun={EntriesPerRun:N0} bgCompactions={BackgroundCompactions}\n" +
               $"  universal: L0trigger={Universal.Level0FileNumCompactionTrigger} minW={Universal.MinMergeWidth} maxW={Universal.MaxMergeWidth} sizeRatio={Universal.SizeRatioPercent} stopSimilarSize={Universal.StopStyleSimilarSize} maxSizeAmp={Universal.MaxSizeAmplificationPercent}\n" +
               $"  db={DbDir}";
    }
}
