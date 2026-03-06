using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddCommandLine(args);

var config = new BundlerRpcServerConfig();
builder.Configuration.Bind("Bundler", config);

if (args.Length > 0 && !args[0].StartsWith("--"))
{
    config.RpcUrl = args[0];
}
if (args.Length > 1 && !args[1].StartsWith("--"))
{
    config.BeneficiaryAddress = args[1];
}
if (args.Length > 2 && !args[2].StartsWith("--"))
{
    config.SupportedEntryPoints = new[] { args[2] };
}

var entryPoint = builder.Configuration["entryPoint"];
if (!string.IsNullOrEmpty(entryPoint))
{
    config.SupportedEntryPoints = new[] { entryPoint };
}

var beneficiary = builder.Configuration["beneficiary"];
if (!string.IsNullOrEmpty(beneficiary))
{
    config.BeneficiaryAddress = beneficiary;
}

var rpcUrl = builder.Configuration["rpc"];
if (!string.IsNullOrEmpty(rpcUrl))
{
    config.RpcUrl = rpcUrl;
}

var chainIdStr = builder.Configuration["chainId"];
if (!string.IsNullOrEmpty(chainIdStr) && BigInteger.TryParse(chainIdStr, out var chainId))
{
    config.ChainId = chainId;
}

var host = builder.Configuration["host"];
if (!string.IsNullOrEmpty(host))
{
    config.Host = host;
}

var portStr = builder.Configuration["port"];
if (!string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out var port))
{
    config.Port = port;
}

var privateKey = builder.Configuration["privateKey"];
if (!string.IsNullOrEmpty(privateKey))
{
    config.PrivateKey = privateKey;
}

config.Verbose = builder.Configuration.GetValue<bool>("verbose", false);
config.EnableDebugMethods = builder.Configuration.GetValue<bool>("debug", false);
config.UnsafeMode = builder.Configuration.GetValue<bool>("unsafe", false);

if (string.IsNullOrEmpty(config.BeneficiaryAddress))
{
    Console.Error.WriteLine("Error: beneficiary address is required");
    Console.Error.WriteLine("Usage: dotnet run -- <rpc-url> <beneficiary> <entrypoint>");
    Console.Error.WriteLine("   Or: dotnet run -- --rpc=<url> --beneficiary=<addr> --entryPoint=<addr>");
    return 1;
}

if (config.SupportedEntryPoints.Length == 0)
{
    Console.Error.WriteLine("Error: at least one entryPoint address is required");
    return 1;
}

builder.Services.AddBundlerRpcServer(config);

if (config.Verbose)
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
    builder.Logging.AddConsole();
}

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();

logger.LogInformation("Starting ERC-4337 Bundler RPC Server");
logger.LogInformation("RPC URL: {RpcUrl}", config.RpcUrl);
logger.LogInformation("Chain ID: {ChainId}", config.ChainId);
logger.LogInformation("Beneficiary: {Beneficiary}", config.BeneficiaryAddress);
logger.LogInformation("Entry Points: {EntryPoints}", string.Join(", ", config.SupportedEntryPoints));
logger.LogInformation("Listening on: http://{Host}:{Port}", config.Host, config.Port);

if (config.EnableDebugMethods)
{
    logger.LogWarning("Debug methods are ENABLED - not recommended for production");
}

if (config.UnsafeMode)
{
    logger.LogWarning("Unsafe mode is ENABLED - validation is relaxed");
}

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

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

        if (json.TrimStart().StartsWith('['))
        {
            var requests = JsonSerializer.Deserialize<RpcRequestMessage[]>(json, jsonOptions);
            if (requests != null)
            {
                var responses = await dispatcher.DispatchBatchAsync(requests);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(responses, jsonOptions));
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
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
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

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    version = "1.0.0",
    chainId = config.ChainId.ToString(),
    supportedEntryPoints = config.SupportedEntryPoints,
    debugEnabled = config.EnableDebugMethods
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run($"http://{config.Host}:{config.Port}");

return 0;
