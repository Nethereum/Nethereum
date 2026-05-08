var builder = DistributedApplication.CreateBuilder(args);

var isTest = string.Equals(builder.Configuration["Testing"], "true", StringComparison.OrdinalIgnoreCase);
var postgresServer = builder.AddPostgres("postgres");
if (!isTest)
{
    postgresServer.WithDataVolume("appchain-pgdata");
}
var postgres = postgresServer.AddDatabase("appchaindb");

var anchorChain = builder.AddProject<Projects.Nethereum_AppChain_AnchorTarget>("anchor-chain");

var appchain = builder.AddProject<Projects.Nethereum_AppChain_Node>("appchain")
    .WithReference(anchorChain)
    .WithReference(postgres)
    .WaitFor(anchorChain)
    .WaitFor(postgres);

var prover = builder.AddProject<Projects.Nethereum_AppChain_Prover>("block-prover")
    .WithReference(appchain)
    .WithReference(postgres)
    .WaitFor(appchain);

var indexer = builder.AddProject<Projects.Nethereum_AppChain_Indexer>("indexer")
    .WithReference(appchain)
    .WithReference(postgres)
    .WaitFor(appchain)
    .WaitFor(postgres);

var explorer = builder.AddProject<Projects.Nethereum_AppChain_Explorer>("explorer")
    .WithReference(appchain)
    .WithReference(postgres)
    .WaitFor(indexer);

builder.Build().Run();
