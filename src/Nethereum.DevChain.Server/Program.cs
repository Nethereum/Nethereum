using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Hosting;
using Nethereum.DevChain.Server.Server;
using Nethereum.JsonRpc.Client.RpcMessages;

if (args.Any(a => a == "--help" || a == "-h" || a == "-?"))
{
    PrintHelp();
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddCommandLine(args);

var config = new DevChainServerConfig();
builder.Configuration.GetSection("DevChain").Bind(config);

ApplyCommandLineOverrides(config, args);

builder.Services.AddDevChainServer(config);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Nethereum", config.Verbose ? LogLevel.Debug : LogLevel.Information);

builder.Services.AddHostedService(sp =>
    new DevChainHostedService(
        sp.GetRequiredService<DevChainNode>(),
        sp.GetRequiredService<DevAccountManager>(),
        sp,
        sp.GetService<ILoggerFactory>()?.CreateLogger<DevChainHostedService>())
    { AlreadyStarted = true });

var app = builder.Build();

var node = app.Services.GetRequiredService<DevChainNode>();
var accountManager = app.Services.GetRequiredService<DevAccountManager>();
var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

await node.StartAsync(accountManager.Accounts.Select(a => a.Address));

PrintBanner(config, accountManager, app);

app.UseCors();

app.MapPost("/", async (HttpContext httpContext) =>
{
    try
    {
        using var reader = new StreamReader(httpContext.Request.Body);
        var json = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(json))
        {
            httpContext.Response.StatusCode = 400;
            await httpContext.Response.WriteAsJsonAsync(new { error = "Empty request body" });
            return;
        }

        httpContext.Response.ContentType = "application/json";

        if (json.TrimStart().StartsWith('['))
        {
            var requests = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequestArray);
            if (requests != null)
            {
                var rpcRequests = requests.Select(ToRpcRequestMessage).ToArray();
                var responses = await dispatcher.DispatchBatchAsync(rpcRequests);
                var jsonResponses = responses.Select(ToJsonRpcResponse).ToArray();
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(jsonResponses, CoreChainJsonContext.Default.JsonRpcResponseArray));
                return;
            }
        }

        var request = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequest);
        if (request == null)
        {
            httpContext.Response.StatusCode = 400;
            await httpContext.Response.WriteAsync("{\"error\":\"Invalid JSON-RPC request\"}");
            return;
        }

        var rpcRequest = ToRpcRequestMessage(request);
        var response = await dispatcher.DispatchAsync(rpcRequest);
        var jsonResponse = ToJsonRpcResponse(response);
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(jsonResponse, CoreChainJsonContext.Default.JsonRpcResponse));
    }
    catch (JsonException ex)
    {
        logger.LogError(ex, "JSON parsing error");
        httpContext.Response.StatusCode = 400;
        httpContext.Response.ContentType = "application/json";
        var errorResponse = new JsonRpcResponse
        {
            Id = null,
            Error = new JsonRpcError { Code = -32700, Message = "Parse error: " + ex.Message }
        };
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, CoreChainJsonContext.Default.JsonRpcResponse));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error");
        httpContext.Response.StatusCode = 500;
        httpContext.Response.ContentType = "application/json";
        var errorMessage = config.Verbose ? $"Internal error: {ex.Message}" : "Internal error";
        var errorResponse = new JsonRpcResponse
        {
            Id = null,
            Error = new JsonRpcError { Code = -32603, Message = errorMessage }
        };
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, CoreChainJsonContext.Default.JsonRpcResponse));
    }
});

var serverVersion = typeof(Program).Assembly.GetName().Version;
app.MapGet("/", () => Results.Ok(new { status = "ok", version = $"Nethereum.DevChain/{serverVersion?.Major}.{serverVersion?.Minor}.{serverVersion?.Build}" }));

app.Run($"http://{config.Host}:{config.Port}");

// ──────────────────────────────────────────────────────────────────
// CLI
// ──────────────────────────────────────────────────────────────────

void PrintHelp()
{
    var version = typeof(Program).Assembly.GetName().Version;
    Console.WriteLine($"Nethereum DevChain Server v{version?.Major}.{version?.Minor}.{version?.Build}");
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
    Console.WriteLine();
    Console.WriteLine("FORK:");
    Console.WriteLine("  -f, --fork <URL>            Fork from a remote RPC endpoint");
    Console.WriteLine("      --fork-block <NUMBER>   Fork at a specific block number");
    Console.WriteLine();
    Console.WriteLine("STORAGE:");
    Console.WriteLine("      --persist [DIR]         Persist chain data to disk (default: ./chaindata)");
    Console.WriteLine("      --in-memory             Use in-memory storage instead of SQLite");
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
    var version = typeof(Program).Assembly.GetName().Version;
    Console.WriteLine($"              DevChain Server v{version?.Major}.{version?.Minor}.{version?.Build}");
    Console.ResetColor();
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  RPC:        http://{config.Host}:{config.Port}");
    Console.ResetColor();
    Console.WriteLine($"  Chain ID:   {config.ChainId}");
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

RpcRequestMessage ToRpcRequestMessage(JsonRpcRequest request)
{
    return new RpcRequestMessage
    {
        Id = request.Id,
        Method = request.Method,
        JsonRpcVersion = request.Jsonrpc,
        RawParameters = request.Params.HasValue ? request.Params.Value : null
    };
}

JsonRpcResponse ToJsonRpcResponse(RpcResponseMessage response)
{
    if (response.HasError)
    {
        return new JsonRpcResponse
        {
            Id = response.Id,
            Error = new JsonRpcError
            {
                Code = response.Error.Code,
                Message = response.Error.Message,
                Data = response.Error.Data
            }
        };
    }

    return new JsonRpcResponse
    {
        Id = response.Id,
        Result = response.Result
    };
}
