using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Server;
using Nethereum.JsonRpc.Client.RpcMessages;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddCommandLine(args);

var config = new DevChainServerConfig();
builder.Configuration.GetSection("DevChain").Bind(config);

ApplyCommandLineOverrides(config, args);

builder.Services.AddDevChainServer(config);
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    if (config.Verbose)
    {
        logging.SetMinimumLevel(LogLevel.Debug);
    }
});

var app = builder.Build();

var node = app.Services.GetRequiredService<DevChainNode>();
var accountManager = app.Services.GetRequiredService<DevAccountManager>();
var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

await node.StartAsync(accountManager.Accounts.Select(a => a.Address));

PrintBanner(config, accountManager);

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

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        if (json.TrimStart().StartsWith('['))
        {
            var requests = JsonSerializer.Deserialize<RpcRequestMessage[]>(json, jsonOptions);
            if (requests != null)
            {
                var responses = await dispatcher.DispatchBatchAsync(requests);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(responses));
                return;
            }
        }

        var request = JsonSerializer.Deserialize<RpcRequestMessage>(json, jsonOptions);
        if (request == null)
        {
            httpContext.Response.StatusCode = 400;
            await httpContext.Response.WriteAsJsonAsync(new { error = "Invalid JSON-RPC request" });
            return;
        }

        var response = await dispatcher.DispatchAsync(request);
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
    catch (JsonException ex)
    {
        logger.LogError(ex, "JSON parsing error");
        httpContext.Response.StatusCode = 400;
        await httpContext.Response.WriteAsJsonAsync(new RpcResponseMessage(null, new RpcError
        {
            Code = -32700,
            Message = "Parse error: " + ex.Message
        }));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error");
        httpContext.Response.StatusCode = 500;
        await httpContext.Response.WriteAsJsonAsync(new RpcResponseMessage(null, new RpcError
        {
            Code = -32603,
            Message = "Internal error: " + ex.Message
        }));
    }
});

var serverVersion = typeof(Program).Assembly.GetName().Version;
app.MapGet("/", () => Results.Ok(new { status = "ok", version = $"Nethereum.DevChain/{serverVersion?.Major}.{serverVersion?.Minor}.{serverVersion?.Build}" }));

app.Run($"http://{config.Host}:{config.Port}");

void PrintBanner(DevChainServerConfig config, DevAccountManager accounts)
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
    Console.WriteLine($"RPC Server listening on http://{config.Host}:{config.Port}");
    Console.ResetColor();
    Console.WriteLine($"Chain ID: {config.ChainId}");

    if (config.Fork?.Url != null)
    {
        Console.WriteLine($"Forking from: {config.Fork.Url}");
        if (config.Fork.BlockNumber.HasValue)
        {
            Console.WriteLine($"Fork block: {config.Fork.BlockNumber}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Available Accounts");
    Console.WriteLine("==================");
    foreach (var account in accounts.Accounts)
    {
        var balanceEth = account.Balance / System.Numerics.BigInteger.Parse("1000000000000000000");
        Console.WriteLine($"({account.Index}) {account.Address} ({balanceEth} ETH)");
    }

    Console.WriteLine();
    Console.WriteLine("Private Keys");
    Console.WriteLine("============");
    foreach (var account in accounts.Accounts)
    {
        Console.WriteLine($"({account.Index}) {account.GetPrivateKeyHex()}");
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("HD Wallet");
    Console.WriteLine("=========");
    Console.WriteLine($"Mnemonic: {config.Mnemonic}");
    Console.WriteLine("Derivation Path: m/44'/60'/0'/0/x");
    Console.ResetColor();
    Console.WriteLine();
}

void ApplyCommandLineOverrides(DevChainServerConfig config, string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i].ToLowerInvariant();

        if ((arg == "--port" || arg == "-p") && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var port))
                config.Port = port;
        }
        else if (arg == "--host" && i + 1 < args.Length)
        {
            config.Host = args[++i];
        }
        else if ((arg == "--chain-id" || arg == "-c") && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var chainId))
                config.ChainId = chainId;
        }
        else if ((arg == "--accounts" || arg == "-a") && i + 1 < args.Length)
        {
            if (int.TryParse(args[++i], out var count))
                config.AccountCount = count;
        }
        else if (arg == "--mnemonic" && i + 1 < args.Length)
        {
            config.Mnemonic = args[++i];
        }
        else if (arg == "--fork" && i + 1 < args.Length)
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
        else if (arg == "--verbose" || arg == "-v")
        {
            config.Verbose = true;
        }
    }
}
