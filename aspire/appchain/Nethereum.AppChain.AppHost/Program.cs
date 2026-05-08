var builder = DistributedApplication.CreateBuilder(args);

var isTest = string.Equals(builder.Configuration["Testing"], "true", StringComparison.OrdinalIgnoreCase);
var postgresServer = builder.AddPostgres("postgres");
if (!isTest)
{
    postgresServer.WithDataVolume("appchain-pgdata");
}
var mainchainDb = postgresServer.AddDatabase("mainchaindb");
var appchainDb = postgresServer.AddDatabase("appchaindb");

var mainchain = builder.AddProject<Projects.Nethereum_AppChain_MainChain>("mainchain");

var appchain = builder.AddProject<Projects.Nethereum_AppChain_Node>("appchain")
    .WithReference(mainchain)
    .WithReference(appchainDb)
    .WaitFor(mainchain)
    .WaitFor(appchainDb);

var anchoring = builder.AddProject<Projects.Nethereum_AppChain_AnchorService>("anchoring")
    .WithReference(mainchain)
    .WithReference(appchain)
    .WithReference(appchainDb)
    .WaitFor(mainchain)
    .WaitFor(appchain);

var prover = builder.AddProject<Projects.Nethereum_AppChain_Prover>("block-prover")
    .WithReference(appchain)
    .WithReference(appchainDb)
    .WaitFor(appchain);

var loadgen = builder.AddProject<Projects.Nethereum_AppChain_LoadGenerator>("loadgenerator")
    .WithReference(appchain)
    .WaitFor(appchain);

var mainchainIndexer = builder.AddProject<Projects.Nethereum_AppChain_MainChain_Indexer>("mainchain-indexer")
    .WithReference(mainchain)
    .WithReference(mainchainDb)
    .WaitFor(mainchain)
    .WaitFor(mainchainDb);

var mainchainExplorer = builder.AddProject<Projects.Nethereum_AppChain_MainChain_Explorer>("mainchain-explorer")
    .WithReference(mainchain)
    .WithReference(mainchainDb)
    .WaitFor(mainchainIndexer);

var appchainIndexer = builder.AddProject<Projects.Nethereum_AppChain_Indexer>("appchain-indexer")
    .WithReference(appchain)
    .WithReference(appchainDb)
    .WaitFor(appchain)
    .WaitFor(appchainDb);

var appchainExplorer = builder.AddProject<Projects.Nethereum_AppChain_Explorer>("appchain-explorer")
    .WithReference(appchain)
    .WithReference(appchainDb)
    .WaitFor(appchainIndexer);

builder.Build().Run();
