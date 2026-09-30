using System.IO;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.MainnetChain.Server;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using RocksDbSharp;

if (args.Any(a => a == "--help" || a == "-h" || a == "-?"))
{
    PrintHelp();
    return;
}

if (args.Any(a => a == "--help-advanced"))
{
    PrintAdvancedHelp();
    return;
}

if (args.Any(a => a == "--alloc-profile"))
    Nethereum.MainnetChain.Server.AllocationProfiler.Start();

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddCommandLine(args);

var config = new MainnetChainServerConfig();
config.SnapBootstrap = true;
builder.Configuration.GetSection("MainnetChain").Bind(config);

MainnetChainCliArgs.Apply(config, args);

if (string.IsNullOrWhiteSpace(config.DataDir))
    config.DataDir = "./mainnet-data";

if (args.Any(a => a == "--align-byhash-cursor"))
{
    using var alignLoggerFactory = LoggerFactory.Create(ConfigureNethereumLogging);
    MainnetNodeComposition.AlignByHashReindexCursorToFreezerHead(
        config, alignLoggerFactory.CreateLogger("Nethereum.MainnetChain"));
    return;
}

if (!config.VerifyFlat)
{
    switch (ConsensusStartupGuard.Evaluate(config))
    {
        case ConsensusStartupGuard.Decision.RefuseNoBeacon:
            Console.Error.WriteLine(ConsensusStartupGuard.RefusalMessage);
            Environment.ExitCode = 1;
            return;
        case ConsensusStartupGuard.Decision.UnverifiedAllowed:
            Console.Error.WriteLine(ConsensusStartupGuard.UnverifiedWarning);
            break;
    }
}

using (var bannerLoggerFactory = LoggerFactory.Create(ConfigureNethereumLogging))
{
    bannerLoggerFactory.CreateLogger("Nethereum.MainnetChain").LogInformation(
        "MainnetChain: snap sync starting — data_dir={DataDir} snap={Snap} beacon={Beacon} metrics={Metrics}",
        config.DataDir,
        config.SnapBootstrap ? "on" : "off",
        string.IsNullOrWhiteSpace(config.LightClient?.BeaconEndpoint) ? "unverified" : "set",
        config.MetricsPort > 0 ? $":{config.MetricsPort}" : "off");
}

builder.AddMainnetChainServer(config);

if (config.MetricsPort > 0)
{
    builder.Services.AddOpenTelemetry().WithMetrics(m => m
        .AddMeter("Nethereum.SnapSync", "Nethereum.SnapSync.Detailed")
        .AddPrometheusExporter());
}

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
ConfigureNethereumLogging(builder.Logging);

try
{
    if (!string.IsNullOrWhiteSpace(config.DataDir))
    {
        var nodeLoggerFactory = LoggerFactory.Create(ConfigureNethereumLogging);
        await builder.Services.AddMainnetNodeAsync(config, nodeLoggerFactory);
    }

    var app = builder.Build();
    app.MapMainnetChainEndpoints();
    app.MapDefaultEndpoints();

    var hostManagesUrls = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));

    app.Lifetime.ApplicationStarted.Register(() => PrintReadyBanner(config, $"http://{config.Host}:{config.Port}"));

    if (config.MetricsPort > 0)
    {
        if (!hostManagesUrls)
        {
            app.Urls.Add($"http://{config.Host}:{config.Port}");
            if (config.MetricsPort != config.Port)
                app.Urls.Add($"http://{config.Host}:{config.MetricsPort}");
        }
        app.MapPrometheusScrapingEndpoint().RequireHost($"*:{config.MetricsPort}");
        app.Run();
    }
    else if (hostManagesUrls)
    {
        app.Run();
    }
    else
    {
        app.Run($"http://{config.Host}:{config.Port}");
    }
}
catch (Exception ex) when (StartupFailureMessage(ex, config) is string friendly)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(friendly);
    Environment.ExitCode = 1;
}

void ConfigureNethereumLogging(ILoggingBuilder lb)
{
    lb.AddConsole(o => o.FormatterName = CompactConsoleFormatter.FormatterName);
    lb.AddConsoleFormatter<CompactConsoleFormatter, ConsoleFormatterOptions>();
    lb.SetMinimumLevel(LogLevel.Warning);
    lb.AddFilter("Nethereum", config.Verbose ? LogLevel.Debug : LogLevel.Information);
    lb.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);
}

static void PrintReadyBanner(MainnetChainServerConfig config, string rpcUrl)
{
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
    Console.WriteLine($"  MainnetChain ready — listening at {rpcUrl}");
    Console.WriteLine($"  Chain:      Ethereum Mainnet");
    Console.WriteLine($"  Data dir:   {config.DataDir ?? "<in-memory>"}");
    Console.WriteLine($"  Sync mode:  {(config.SnapBootstrap ? "snap" : "full")}");
    Console.WriteLine($"  Consensus:  {(string.IsNullOrWhiteSpace(config.LightClient?.BeaconEndpoint) ? "unverified (no beacon)" : $"verified (beacon {config.LightClient!.BeaconEndpoint})")}");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
    Console.WriteLine();
}

static string? StartupFailureMessage(Exception ex, MainnetChainServerConfig config)
{
    var socketFailure = FirstOfType<SocketException>(ex);
    if (socketFailure != null && socketFailure.SocketErrorCode == SocketError.AddressAlreadyInUse)
        return
            $"MainnetChain failed to start: port {config.Port} is already in use." + Environment.NewLine +
            "Another process — perhaps another MainnetChain instance — is already listening there." + Environment.NewLine +
            "Stop it, or pick a different port with --port.";

    var rocksDbFailure = FirstOfType<RocksDbException>(ex);
    if (rocksDbFailure != null)
        return
            $"MainnetChain failed to start: could not open the chain data at '{config.DataDir}'." + Environment.NewLine +
            "This usually means another MainnetChain process already has that data directory open." + Environment.NewLine +
            "Stop it, or point --data-dir at a different directory." + Environment.NewLine +
            $"({rocksDbFailure.Message})";

    return null;
}

static TException? FirstOfType<TException>(Exception ex) where TException : Exception
{
    for (var current = ex; current != null; current = current.InnerException)
        if (current is TException match)
            return match;

    return null;
}

static void PrintHelp()
{
    Console.WriteLine($"Nethereum MainnetChain Server v{Nethereum.CoreChain.NodeVersion.Version}");
    Console.WriteLine("Read-only Ethereum mainnet follower: syncs, validates, and serves mainnet over JSON-RPC.");
    Console.WriteLine();
    Console.WriteLine("USAGE: nethereum-mainnetchain [OPTIONS]");
    Console.WriteLine();
    Console.WriteLine("SERVER:");
    Console.WriteLine("  -p, --port <PORT>          JSON-RPC port (default: 8545)");
    Console.WriteLine("      --host <HOST>          Host to bind to (default: 127.0.0.1)");
    Console.WriteLine("  -v, --verbose              Verbose logging");
    Console.WriteLine("      --metrics-port <PORT>  Expose Prometheus /metrics on this port (default: off)");
    Console.WriteLine();
    Console.WriteLine("SYNC:");
    Console.WriteLine("  -d, --data-dir <DIR>       Chain data directory (default: ./mainnet-data)");
    Console.WriteLine("      --no-snap              Skip snap sync (snap is the default; a full sync is impractical for mainnet)");
    Console.WriteLine("      --trusted-peer <ENODE> Pinned enode:// peer to always dial");
    Console.WriteLine("      --listen-port <PORT>   Also serve snap/eth to other peers (default: off)");
    Console.WriteLine();
    Console.WriteLine("CONSENSUS:");
    Console.WriteLine("      --beacon <URL>         Beacon REST endpoint (enables the light-client gate)");
    Console.WriteLine("      --allow-unverified-consensus  Start WITHOUT a beacon (no consensus verification; NOT recommended for mainnet)");
    Console.WriteLine();
    Console.WriteLine("MAINTENANCE:");
    Console.WriteLine("      --wipe-state           One-shot: clear state and re-run the state sync, keeping the block archive");
    Console.WriteLine("      --rebuild-state-from-flat  One-shot: rebuild stale storage-trie nodes from flat state, then verify");
    Console.WriteLine("      --verify-flat          One-shot: write-free flat/trie verify at the committed head, logs the full breakdown, then stops");
    Console.WriteLine("      --verify-flat-sample <N>  With --verify-flat: verify only the first N accounts per shard (0 = full walk, default)");
    Console.WriteLine("      --node-key-file <PATH> Persisted node identity key (default: <data-dir>/nodekey, created on first run)");
    Console.WriteLine();
    Console.WriteLine("TUNING (common advanced):");
    Console.WriteLine("      --flush-cadence <N>    Persist state every N blocks (default 1 = every block)");
    Console.WriteLine("      --checkpoint-every <N> Checkpoint cadence in blocks (default 50000)");
    Console.WriteLine("      --cache-size <SIZE>    RocksDB block cache size, e.g. 4G / 512M (default 1G)");
    Console.WriteLine();
    Console.WriteLine("Settings can also come from appsettings.json (MainnetChain section) or MainnetChain__ env vars.");
    Console.WriteLine("Run with --help-advanced to list all expert MainnetChain:* settings.");
}

static void PrintAdvancedHelp()
{
    Console.WriteLine($"Nethereum MainnetChain Server v{Nethereum.CoreChain.NodeVersion.Version}");
    Console.WriteLine("Expert MainnetChain:* settings — raw form only (or, where noted above, a friendly flag).");
    Console.WriteLine();
    Console.WriteLine("USAGE: nethereum-mainnetchain --MainnetChain:<Name> <value> [...]");
    Console.WriteLine();
    Console.WriteLine("      --MainnetChain:StartBlock <ulong>                    First block to sync from   (default: 1)");
    Console.WriteLine("      --MainnetChain:Blocks <ulong>                        Number of blocks to sync   (default: unlimited)");
    Console.WriteLine("      --MainnetChain:TargetPeers <int>                     Peer pool target size   (default: 16)");
    Console.WriteLine("      --MainnetChain:HeadersBatch <int>                    Header fetch batch size   (default: 192)");
    Console.WriteLine("      --MainnetChain:BodiesBatch <int>                     Body fetch batch size   (default: 64)");
    Console.WriteLine("      --MainnetChain:CheckpointEvery <ulong>               Checkpoint cadence in blocks   (default: 50000)");
    Console.WriteLine("      --MainnetChain:KeepLatestCheckpoints <int>           Checkpoints retained on disk   (default: 5)");
    Console.WriteLine("      --MainnetChain:JournalBlocks <int>                   Value-history retention window   (default: 128)");
    Console.WriteLine("      --MainnetChain:PathKeyedState <bool>                 Path-keyed (vs legacy hash-keyed) state storage   (default: true)");
    Console.WriteLine("      --MainnetChain:TrieNodeHistoryBlocks <int>           Trie-node history retention window   (default: 128)");
    Console.WriteLine("      --MainnetChain:TrieNodeHistoryIndex <bool>           Key-major index over the node history   (default: true)");
    Console.WriteLine("      --MainnetChain:BlockCacheSize <long>                 RocksDB shared block cache, in bytes   (default: 1073741824)");
    Console.WriteLine("      --MainnetChain:FlushCadenceBlocks <int>              Persist state every N blocks   (default: 1)");
    Console.WriteLine("      --MainnetChain:BulkSync <bool>                       WAL-off bulk-save backfill path   (default: false)");
    Console.WriteLine("      --MainnetChain:SplitHistoryStore <bool>              Split storage into core/history physical DBs   (default: false)");
    Console.WriteLine("      --MainnetChain:HotWindowBlocks <int>                 Core self-sufficiency rolling hot-window size, in blocks (SplitHistoryStore only)   (default: 128)");
    Console.WriteLine("      --MainnetChain:EnableLogIndex <bool>                 Build/serve getLogs from the legacy CF_LOGS index instead of the bloom-scan L1   (default: false)");
    Console.WriteLine("      --MainnetChain:UseFreezerHistory <bool>              Route bulk-sync history into the append-only geth-format freezer (needs FreezerHistoryDirectory)   (default: false)");
    Console.WriteLine("      --MainnetChain:FreezerHistoryDirectory <string>      Directory for the freezer archive (required when UseFreezerHistory is set)   (default: unset)");
    Console.WriteLine("      --MainnetChain:DisableDiscv5 <bool>                  Disable discv5 peer discovery   (default: false)");
    Console.WriteLine("      --MainnetChain:Discv5Port <int>                      discv5 UDP port   (default: 0)");
    Console.WriteLine("      --MainnetChain:ContinueOnMismatch <bool>             Continue past a state-root mismatch instead of halting   (default: false)");
    Console.WriteLine("      --MainnetChain:SnapPhase1Only <bool>                 Run only the Phase 1 block archive, then stop   (default: false)");
    Console.WriteLine("      --MainnetChain:SnapPhase1First <bool>                Complete Phase 1 before starting Phase 2   (default: false)");
    Console.WriteLine("      --MainnetChain:BackwardSkeletonPhase1 <bool>         Backward header skeleton for Phase 1   (default: true)");
    Console.WriteLine("      --MainnetChain:HeadersFrom <ulong>                   Override: start the header sweep here   (default: unset = cursor-driven)");
    Console.WriteLine("      --MainnetChain:HeadersTo <ulong>                     Floor for the header sweep override   (default: 0)");
    Console.WriteLine("      --MainnetChain:ReceiptBackfill <bool>                Re-fetch and re-validate stored receipts in the background   (default: false)");
    Console.WriteLine("      --MainnetChain:RpcMaxLogBlockRange <int>             eth_getLogs/eth_getFilterLogs max block range, 0=uncapped   (default: 10000)");
    Console.WriteLine("      --MainnetChain:RpcMaxLogResults <int>                eth_getLogs/eth_getFilterLogs max result count, 0=uncapped   (default: 10000)");
    Console.WriteLine("      --MainnetChain:RpcGasCap <long>                      eth_call/estimateGas/traceCall gas cap, 0=uncapped   (default: 50000000)");
    Console.WriteLine();
    Console.WriteLine("Run with --help for the everyday flags.");
}
