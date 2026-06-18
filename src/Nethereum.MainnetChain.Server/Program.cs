using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.MainnetChain.Server.Hosting;

if (args.Any(a => a == "--help" || a == "-h" || a == "-?"))
{
    PrintHelp();
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddCommandLine(args);

var config = new MainnetChainServerConfig();
builder.Configuration.GetSection("MainnetChain").Bind(config);

builder.AddMainnetChainServer(config);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Nethereum", config.Verbose ? LogLevel.Debug : LogLevel.Information);

var app = builder.Build();
app.MapMainnetChainEndpoints();

app.Run($"http://{config.Host}:{config.Port}");

static void PrintHelp()
{
    var version = typeof(Program).Assembly.GetName().Version;
    Console.WriteLine($"Nethereum MainnetChain Server v{version?.Major}.{version?.Minor}.{version?.Build}");
    Console.WriteLine("Read-only mainnet follower with JSON-RPC server + optional beacon LightClient consensus gate");
    Console.WriteLine();
    Console.WriteLine("USAGE: nethereum-mainnetchain [OPTIONS]");
    Console.WriteLine();
    Console.WriteLine("Configure via appsettings.json (MainnetChain section) or environment variables prefixed MainnetChain__.");
    Console.WriteLine();
    Console.WriteLine("Key settings:");
    Console.WriteLine("  Host, Port            HTTP bind address (default 127.0.0.1:8545)");
    Console.WriteLine("  DataDir               RocksDB chain data directory (required for production sync)");
    Console.WriteLine("  TrustedPeer           Optional pinned enode://… peer");
    Console.WriteLine("  StartBlock, Blocks    Replay window");
    Console.WriteLine("  LightClient:");
    Console.WriteLine("    BeaconEndpoint      Beacon REST URL (enables consensus gate)");
    Console.WriteLine("    WeakSubjectivityRoot  32-byte trusted bootstrap root (hex)");
    Console.WriteLine("    GenesisValidatorsRoot 32-byte genesis validators root (hex)");
}
