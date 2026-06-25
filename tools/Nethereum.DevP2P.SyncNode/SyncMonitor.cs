using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// External observer for a live <see cref="Program"/> sync. Reads the
    /// <c>sync-status.json</c> file the ValidationReporter writes into the
    /// shared <c>--data-dir</c> and contacts the same canonical RPC the sync
    /// uses to report canonical-head lag, peer count, and divergence count
    /// every 30s. Never opens RocksDB (the writer is exclusive), never
    /// touches any other state — strictly read-only file + RPC polling.
    ///
    /// Entrypoint dispatched from <see cref="Program.Main"/> when the first
    /// arg is <c>monitor</c>.
    /// </summary>
    internal static class SyncMonitor
    {
        public static async Task<int> RunAsync(string[] argv)
        {
            string dataDir = null;
            string canonicalRpc = null;
            int intervalSeconds = 30;

            for (int i = 0; i < argv.Length; i++)
            {
                switch (argv[i])
                {
                    case "--data-dir":
                        if (i + 1 >= argv.Length) { Console.Error.WriteLine("missing value for --data-dir"); return 64; }
                        dataDir = argv[++i]; break;
                    case "--canonical-rpc":
                        if (i + 1 >= argv.Length) { Console.Error.WriteLine("missing value for --canonical-rpc"); return 64; }
                        canonicalRpc = argv[++i]; break;
                    case "--interval-seconds":
                        if (i + 1 >= argv.Length) { Console.Error.WriteLine("missing value for --interval-seconds"); return 64; }
                        intervalSeconds = int.Parse(argv[++i]); break;
                    case "--help":
                    case "-h":
                        PrintUsage(); return 0;
                    default:
                        Console.Error.WriteLine($"unknown arg: {argv[i]}");
                        PrintUsage();
                        return 64;
                }
            }

            if (string.IsNullOrWhiteSpace(dataDir))
            {
                Console.Error.WriteLine("error: --data-dir required");
                PrintUsage();
                return 64;
            }

            string statusFile = Path.Combine(dataDir, "sync-status.json");
            EthBlockNumber getBlockNumber = null;
            string rpcHost = null;
            if (!string.IsNullOrWhiteSpace(canonicalRpc))
            {
                try
                {
                    var uri = new Uri(canonicalRpc);
                    rpcHost = uri.Host;
                    var client = new RpcClient(uri);
                    getBlockNumber = new EthBlockNumber(client);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"warning: --canonical-rpc invalid ({ex.Message}); continuing without canonical lag.");
                }
            }

            Console.WriteLine("Nethereum SyncMonitor — external observer");
            Console.WriteLine($"  data-dir       = {dataDir}");
            Console.WriteLine($"  status-file    = {statusFile}");
            Console.WriteLine($"  canonical-rpc  = {(rpcHost ?? "(none)")}");
            Console.WriteLine($"  poll-interval  = {intervalSeconds}s");
            Console.WriteLine(new string('-', 80));

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            while (!cts.IsCancellationRequested)
            {
                var status = ReadStatusFile(statusFile);
                ulong canonicalHead = 0;
                string canonicalNote = "(canonical unavailable)";
                if (getBlockNumber != null)
                {
                    try
                    {
                        var hex = await getBlockNumber.SendRequestAsync().ConfigureAwait(false);
                        canonicalHead = (ulong)hex.Value;
                        canonicalNote = $"canonical={canonicalHead:N0}";
                    }
                    catch (Exception ex)
                    {
                        canonicalNote = $"canonical=ERR ({ex.GetType().Name})";
                    }
                }

                if (status == null)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] status file not yet written: {statusFile}  {canonicalNote}");
                }
                else
                {
                    string lagStr = canonicalHead > 0
                        ? $"lag={(long)canonicalHead - (long)status.LastBlock:N0}"
                        : "lag=?";
                    Console.WriteLine(
                        $"[{DateTime.UtcNow:HH:mm:ss}] local={status.LastBlock:N0}  {canonicalNote}  {lagStr}  peers={status.Peers}  blocks/sec={status.BlocksPerSec:F1}  divergences={status.Divergences}");
                    if (status.Divergences > 0)
                    {
                        Console.WriteLine(
                            $"  !! last divergence: block={status.LastDivergenceBlock:N0}  {status.LastDivergenceDetail}");
                    }
                    if (canonicalHead > 0 && canonicalHead > status.LastBlock + 100_000)
                    {
                        Console.WriteLine("  WARNING: canonical lag > 100k blocks");
                    }
                }

                try { await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }

            Console.WriteLine("SyncMonitor: cancelled.");
            return 0;
        }

        private sealed class StatusSnapshot
        {
            public ulong LastBlock { get; set; }
            public ulong BlocksExecuted { get; set; }
            public ulong Divergences { get; set; }
            public ulong LastDivergenceBlock { get; set; }
            public string LastDivergenceDetail { get; set; }
            public int Peers { get; set; }
            public double BlocksPerSec { get; set; }
            public string TimestampUtc { get; set; }
        }

        private static StatusSnapshot ReadStatusFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw)) return null;
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                return new StatusSnapshot
                {
                    LastBlock = root.TryGetProperty("lastBlock", out var lb) ? (ulong)lb.GetInt64() : 0,
                    BlocksExecuted = root.TryGetProperty("blocksExecuted", out var be) ? (ulong)be.GetInt64() : 0,
                    Divergences = root.TryGetProperty("divergences", out var d) ? (ulong)d.GetInt64() : 0,
                    LastDivergenceBlock = root.TryGetProperty("lastDivergenceBlock", out var ldb) ? (ulong)ldb.GetInt64() : 0,
                    LastDivergenceDetail = root.TryGetProperty("lastDivergenceDetail", out var ldd) ? ldd.GetString() : null,
                    Peers = root.TryGetProperty("peers", out var p) ? p.GetInt32() : 0,
                    BlocksPerSec = root.TryGetProperty("blocksPerSec", out var bps) ? bps.GetDouble() : 0,
                    TimestampUtc = root.TryGetProperty("timestampUtc", out var ts) ? ts.GetString() : null
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: nethereum-syncnode monitor --data-dir PATH [--canonical-rpc URL] [--interval-seconds N]");
            Console.WriteLine();
            Console.WriteLine("  --data-dir PATH         RocksDB data dir of a running sync (read sync-status.json from here).");
            Console.WriteLine("  --canonical-rpc URL     Trusted JSON-RPC to compare canonical head against local head.");
            Console.WriteLine("  --interval-seconds N    Poll cadence (default 30).");
        }
    }
}
