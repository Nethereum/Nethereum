using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.EVM;
using Nethereum.ChainNode.Hosting;
using Nethereum.DevChain;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Composition;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.DevChain.Server.Metrics;
using OpenTelemetry.Metrics;

if (args.Any(a => a == "--help" || a == "-h" || a == "-?"))
{
    PrintHelp();
    return;
}

if (DevChainCli.TryHandleAdvancedHelp(args, Console.Out))
{
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddCommandLine(args);

var config = new DevChainServerConfig();
builder.Configuration.GetSection("DevChain").Bind(config);

ApplyCommandLineOverrides(config, args);
RefuseUnknownHardfork(config);
config.Chain.Registry = Nethereum.EVM.Precompiles.Bls.Bls12381AwareMainnetHardforkRegistry.Build(
    Nethereum.EVM.Precompiles.Kzg.KzgAwareMainnetHardforkRegistry.Instance,
    new Nethereum.Signer.Bls.Herumi.Bls12381Operations());

try
{
    DevChainServerConfigValidator.Validate(config);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"DevChain failed to start: {ex.Message}");
    Environment.Exit(1);
}

if (ChainNodeMaintenanceRunner.AnyRequested(config.Node.Maintenance))
{
    Environment.Exit(await RunMaintenanceAsync(config));
    return;
}

try
{
    builder.AddDevChainServer(config);
    builder.AddPrometheusMetrics(config.Node.Rpc.MetricsPort);
    if (config.Node.Rpc.MetricsPort > 0)
        builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter("Nethereum.DevChain"));
    builder.WebHost.ConfigureKestrel(options =>
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.Logging.AddFilter("Nethereum", config.Verbose ? LogLevel.Debug : LogLevel.Information);
    builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);

    var app = builder.Build();

    var node = app.Services.GetRequiredService<DevChainNode>();
    var accountManager = app.Services.GetRequiredService<DevAccountManager>();

    if (config.Node.Rpc.MetricsPort > 0)
        _ = new DevChainNodeMetrics(node);

    await node.StartAsync(accountManager.Accounts.Select(a => a.Address));

    PrintBanner(config, accountManager, app);

    await app.MapDevChainEndpointsAsync();

    app.MapPrometheusMetrics(config.Host, config.Port, config.Node.Rpc.MetricsPort);

    if (config.Node.Rpc.MetricsPort > 0)
        app.Run();
    else
        app.Run($"http://{config.Host}:{config.Port}");
}
catch (Exception ex) when (StartupFailureMessage(ex, config) is string friendly)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(friendly);
    Environment.Exit(1);
}

// ──────────────────────────────────────────────────────────────────
// CLI
// ──────────────────────────────────────────────────────────────────

async Task<int> RunMaintenanceAsync(DevChainServerConfig config)
{
    using var loggerFactory = LoggerFactory.Create(logging =>
    {
        logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "[HH:mm:ss.fff] ";
            options.SingleLine = true;
        });
        logging.SetMinimumLevel(LogLevel.Information);
    });
    var logger = loggerFactory.CreateLogger("Nethereum.DevChain.Server");

    var (bundle, backendHandle) = DevChainStorageBackend.Open(config);
    try
    {
        return await ChainNodeMaintenanceRunner.RunAndReportExitCodeAsync(
            bundle, config.Node.Maintenance, logger, CancellationToken.None);
    }
    finally
    {
        await backendHandle.DisposeAsync();
    }
}

void RefuseUnknownHardfork(DevChainServerConfig config)
{
    try
    {
        HardforkNames.Parse(config.Hardfork);
    }
    catch (ArgumentException)
    {
        Console.Error.WriteLine($"Unknown hardfork '{config.Hardfork}'. Known forks are named by HardforkName.");
        Environment.Exit(1);
    }
}

string? StartupFailureMessage(Exception ex, DevChainServerConfig config)
{
    var socketFailure = FirstOfType<SocketException>(ex);
    if (socketFailure != null && socketFailure.SocketErrorCode == SocketError.AddressAlreadyInUse)
        return
            $"DevChain failed to start: port {config.Port} is already in use." + Environment.NewLine +
            "Another process — perhaps another DevChain instance — is already listening there." + Environment.NewLine +
            "Stop it, or pick a different port with --port.";

    var sqliteFailure = FirstOfType<Microsoft.Data.Sqlite.SqliteException>(ex);
    if (sqliteFailure != null)
        return
            $"DevChain failed to start: could not open the chain data at '{config.DataDir}'." + Environment.NewLine +
            "This usually means another DevChain process already has that data directory open." + Environment.NewLine +
            "Stop it, or point --persist at a different directory." + Environment.NewLine +
            $"({sqliteFailure.Message})";

    return null;
}

TException? FirstOfType<TException>(Exception ex) where TException : Exception
{
    for (var current = ex; current != null; current = current.InnerException)
        if (current is TException match)
            return match;

    return null;
}

void PrintHelp()
{
    Console.WriteLine($"Nethereum DevChain Server v{Nethereum.CoreChain.NodeVersion.Version}");
    Console.WriteLine("Local Ethereum development node with JSON-RPC server");
    Console.WriteLine();
    Console.WriteLine("USAGE: nethereum-devchain [OPTIONS]");
    Console.WriteLine();
    Console.WriteLine("SERVER:");
    Console.WriteLine("  -p, --port <PORT>           Port to listen on (default: 8545)");
    Console.WriteLine("      --host <HOST>           Host to bind to (default: 127.0.0.1)");
    Console.WriteLine("  -v, --verbose               Enable verbose RPC logging");
    Console.WriteLine();
    Console.WriteLine("ACCOUNTS:");
    Console.WriteLine("  -a, --accounts <NUM>        Number of accounts to generate (default: 10)");
    Console.WriteLine("  -m, --mnemonic <MNEMONIC>   HD wallet mnemonic phrase");
    Console.WriteLine("  -e, --balance <ETH>         Account balance in ETH (default: 10000)");
    Console.WriteLine();
    Console.WriteLine("CHAIN:");
    Console.WriteLine("  -c, --chain-id <ID>         Chain ID (default: 31337)");
    Console.WriteLine("  -b, --block-time <MS>       Block time in ms, 0 = auto-mine (default: 0)");
    Console.WriteLine("      --gas-limit <GAS>       Block gas limit (default: 30000000)");
    Console.WriteLine($"      --hardfork <NAME>       Hardfork to run (default: {DevChainServerConfig.DefaultServerHardfork})");
    Console.WriteLine();
    Console.WriteLine("FORK:");
    Console.WriteLine("  -f, --fork <URL>            Fork from a remote RPC endpoint");
    Console.WriteLine("      --fork-block <NUMBER>   Fork at a specific block number");
    Console.WriteLine();
    Console.WriteLine("STORAGE:");
    Console.WriteLine("      --persist [DIR]         Persist chain data to disk (default: ./chaindata)");
    Console.WriteLine("      --in-memory             Use in-memory storage instead of SQLite");
    Console.WriteLine("      --rocksdb               Use RocksDB storage with path-based state, same as mainnet");
    Console.WriteLine("      --data-dir <DIR>        Data directory for --rocksdb (default: ./chaindata)");
    Console.WriteLine();
    Console.WriteLine("  Default storage is SQLite with auto-cleanup on exit.");
    Console.WriteLine("  Use --persist to keep data between restarts.");
    Console.WriteLine();
    Console.WriteLine("EXAMPLES:");
    Console.WriteLine("  nethereum-devchain");
    Console.WriteLine("  nethereum-devchain -p 8546 -a 20 -e 100");
    Console.WriteLine("  nethereum-devchain -f https://eth.llamarpc.com --fork-block 19000000");
    Console.WriteLine("  nethereum-devchain --persist ./mychain");
    Console.WriteLine("  nethereum-devchain -b 1000 -c 1234");
    Console.WriteLine();
    Console.WriteLine("Run with --help-advanced for the expert --DevChain:Node:* settings.");
}

void ApplyCommandLineOverrides(DevChainServerConfig config, string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i].ToLowerInvariant();

        // Server
        if ((arg == "--port" || arg == "-p") && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var port))
                config.Port = port;
        }
        else if (arg == "--host" && i + 1 < args.Length)
        {
            config.Host = args[++i];
        }
        else if (arg == "--verbose" || arg == "-v")
        {
            config.Verbose = true;
        }

        // Accounts
        else if ((arg == "--accounts" || arg == "-a") && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var count))
                config.AccountCount = count;
        }
        else if ((arg == "--mnemonic" || arg == "-m") && i + 1 < args.Length)
        {
            config.Mnemonic = args[++i];
        }
        else if ((arg == "--balance" || arg == "-e") && i + 1 < args.Length)
        {
            config.SetAccountBalanceEth(args[++i]);
        }

        // Chain
        else if ((arg == "--chain-id" || arg == "-c") && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var chainId))
                config.ChainId = chainId;
        }
        else if ((arg == "--block-time" || arg == "-b") && i + 1 < args.Length)
        {
            if (long.TryParse(args[++i], out var blockTime))
            {
                config.BlockTime = blockTime;
                config.AutoMine = blockTime == 0;
            }
        }
        else if (arg == "--gas-limit" && i + 1 < args.Length)
        {
            if (long.TryParse(args[++i], out var gasLimit))
                config.BlockGasLimit = gasLimit;
        }
        else if (arg == "--hardfork" && i + 1 < args.Length)
        {
            config.Hardfork = args[++i];
        }

        // Fork
        else if ((arg == "--fork" || arg == "-f") && i + 1 < args.Length)
        {
            config.Fork ??= new ForkConfig();
            config.Fork.Url = args[++i];
        }
        else if (arg == "--fork-block" && i + 1 < args.Length)
        {
            config.Fork ??= new ForkConfig();
            if (long.TryParse(args[++i], out var block))
                config.Fork.BlockNumber = block;
        }

        // Storage
        else if (arg == "--persist")
        {
            config.Persist = true;
            config.Storage = "sqlite";
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                config.DataDir = args[++i];
            }
        }
        else if (arg == "--in-memory")
        {
            config.Storage = "memory";
        }
        else if (arg == "--rocksdb")
        {
            config.Storage = "rocksdb";
        }

        // Legacy aliases (kept for backwards compat, hidden from help)
        else if (arg == "--storage" && i + 1 < args.Length)
        {
            config.Storage = args[++i];
        }
        else if (arg == "--data-dir" && i + 1 < args.Length)
        {
            config.DataDir = args[++i];
        }
        else if (arg == "--batch-size" && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var batchSize))
                config.AutoMineBatchSize = batchSize;
        }
        else if (arg == "--batch-timeout" && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var timeoutMs))
                config.AutoMineBatchTimeoutMs = timeoutMs;
        }
        else if (arg == "--max-tx-per-block" && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var maxTx))
                config.MaxTransactionsPerBlock = maxTx;
        }
    }
}

void PrintBanner(DevChainServerConfig config, DevAccountManager accounts, WebApplication app)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(@" _   _      _   _");
    Console.WriteLine(@"| \ | | ___| |_| |__   ___ _ __ ___ _   _ _ __ ___");
    Console.WriteLine(@"|  \| |/ _ \ __| '_ \ / _ \ '__/ _ \ | | | '_ ` _ \");
    Console.WriteLine(@"| |\  |  __/ |_| | | |  __/ | |  __/ |_| | | | | | |");
    Console.WriteLine(@"|_| \_|\___|\__|_| |_|\___|_|  \___|\__,_|_| |_| |_|");
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"              DevChain Server v{Nethereum.CoreChain.NodeVersion.Version}");
    Console.ResetColor();
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  RPC:        http://{config.Host}:{config.Port}");
    Console.ResetColor();
    Console.WriteLine($"  Chain ID:   {config.ChainId}");
    Console.WriteLine($"  Hardfork:   {config.Hardfork}");
    Console.WriteLine($"  Gas Limit:  {config.BlockGasLimit:N0}");

    if (config.BlockTime > 0)
        Console.WriteLine($"  Block Time: {config.BlockTime}ms (interval mining)");
    else
        Console.WriteLine($"  Mining:     auto-mine (instant)");

    var storageMode = config.Storage?.ToLowerInvariant() ?? "sqlite";
    if (storageMode == "memory")
    {
        Console.WriteLine($"  Storage:    in-memory");
    }
    else if (storageMode == "rocksdb")
    {
        Console.WriteLine($"  Storage:    RocksDB, path-based state ({config.DataDir})");
    }
    else
    {
        var sqliteMgr = app.Services.GetService<Nethereum.DevChain.Storage.Sqlite.SqliteStorageManager>();
        if (config.Persist)
            Console.WriteLine($"  Storage:    SQLite (persist: {sqliteMgr?.DbPath ?? config.DataDir})");
        else
            Console.WriteLine($"  Storage:    SQLite (auto-cleanup on exit)");
    }

    if (config.Fork?.Url != null)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"  Fork:       {config.Fork.Url}");
        if (config.Fork.BlockNumber.HasValue)
            Console.Write($" @ block {config.Fork.BlockNumber}");
        Console.WriteLine();
        Console.ResetColor();
    }

    Console.WriteLine();
    Console.WriteLine("  Available Accounts");
    Console.WriteLine("  ==================");
    foreach (var account in accounts.Accounts)
    {
        var balanceEth = account.Balance / System.Numerics.BigInteger.Parse("1000000000000000000");
        Console.WriteLine($"  ({account.Index}) {account.Address} ({balanceEth} ETH)");
    }

    Console.WriteLine();
    Console.WriteLine("  Private Keys");
    Console.WriteLine("  ============");
    foreach (var account in accounts.Accounts)
    {
        Console.WriteLine($"  ({account.Index}) {account.GetPrivateKeyHex()}");
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  HD Wallet");
    Console.WriteLine("  =========");
    Console.WriteLine($"  Mnemonic:        {config.Mnemonic}");
    Console.WriteLine("  Derivation Path: m/44'/60'/0'/0/x");
    Console.ResetColor();
    Console.WriteLine();
}
