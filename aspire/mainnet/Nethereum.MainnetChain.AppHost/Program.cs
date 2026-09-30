var builder = DistributedApplication.CreateBuilder(args);

// The mainnet follower is self-contained: it persists chain data to RocksDB under its own data
// directory, so (unlike the AppChain host) there is no Postgres or other backing service to add.
var mainnet = builder.AddProject<Projects.Nethereum_MainnetChain_Server>("mainnet")
    .WithEnvironment("MainnetChain__DataDir", "./mainnet-data")
    // No beacon light client is configured here, so ConsensusStartupGuard would REFUSE to start
    // (a beacon-less follower accepts whatever its peers serve, with no consensus verification).
    // Allow it explicitly so the node launches in the dashboard for local exploration.
    // A real deployment sets a beacon endpoint instead — .WithEnvironment("MainnetChain__LightClient__BeaconEndpoint", "<URL>")
    // (equivalently, --beacon <URL>) — which makes the gate light-client verified and drops this flag.
    .WithEnvironment("MainnetChain__AllowUnverifiedConsensus", "true");

builder.Build().Run();
