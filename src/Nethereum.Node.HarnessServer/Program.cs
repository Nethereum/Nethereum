using Nethereum.Node.HarnessServer;

if (args.Any(a => a is "--help" or "-h" or "-?"))
{
    PrintHelp();
    return;
}

var options = HarnessCliArgs.Parse(args);
var app = await HarnessNodeHost.BuildAsync(options, args);
await app.RunAsync();

static void PrintHelp()
{
    Console.WriteLine("Nethereum node entrypoint for benchmark/conformance harnesses (benchmarkoor, hive)");
    Console.WriteLine();
    Console.WriteLine("USAGE: nethereum-harness [OPTIONS]");
    Console.WriteLine();
    Console.WriteLine("      --datadir <DIR>              Chain data directory (default: /data)");
    Console.WriteLine("      --genesis <PATH>              Standard EL genesis.json (default: /network-config/genesis.json)");
    Console.WriteLine("      --http.addr <ADDR>            JSON-RPC bind address (default: 127.0.0.1)");
    Console.WriteLine("      --http.port <PORT>            JSON-RPC port (default: 8545)");
    Console.WriteLine("      --authrpc.addr <ADDR>         Engine API bind address (default: 127.0.0.1)");
    Console.WriteLine("      --authrpc.port <PORT>         Engine API port (default: 8551)");
    Console.WriteLine("      --authrpc.jwtsecret <PATH>    Engine API JWT secret file (default: <datadir>/jwt.hex)");
    Console.WriteLine("      --verbose                     Verbose logging");
}
