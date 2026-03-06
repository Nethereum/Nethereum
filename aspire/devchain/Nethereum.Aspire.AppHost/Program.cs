var builder = DistributedApplication.CreateBuilder(args);

var isTest = string.Equals(builder.Configuration["Testing"], "true", StringComparison.OrdinalIgnoreCase);
var postgresServer = builder.AddPostgres("postgres");
if (!isTest)
{
    postgresServer.WithDataVolume("nethereum-pgdata");
}
var postgres = postgresServer.AddDatabase("nethereumdb");

var devchain = builder.AddProject<Projects.Nethereum_Aspire_DevChain>("devchain");

var loadgenerator = builder.AddProject<Projects.Nethereum_Aspire_LoadGenerator>("loadgenerator")
    .WithReference(devchain)
    .WaitFor(devchain);

var indexer = builder.AddProject<Projects.Nethereum_Aspire_Indexer>("indexer")
    .WithReference(postgres)
    .WithReference(devchain)
    .WithReference(loadgenerator)
    .WaitFor(devchain)
    .WaitFor(postgres);

var bundler = builder.AddProject<Projects.Nethereum_Aspire_Bundler>("bundler")
    .WithReference(devchain)
    .WaitFor(devchain);

var devAccountKey = builder.AddParameter("devAccountPrivateKey", secret: true);

var contractsOutPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "contracts", "out"));
var contractsSrcPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "contracts"));

var explorer = builder.AddProject<Projects.Nethereum_Aspire_Explorer>("explorer")
    .WithReference(postgres)
    .WithReference(devchain)
    .WithReference(loadgenerator)
    .WaitFor(indexer)
    .WithEnvironment("Explorer__DevAccountPrivateKey", devAccountKey)
    .WithEnvironment("Explorer__EnablePendingTransactions", "true")
    .WithEnvironment("Explorer__AbiSources__LocalStorageEnabled", "true")
    .WithEnvironment("Explorer__AbiSources__LocalStoragePath", contractsOutPath)
    .WithEnvironment("Explorer__AbiSources__SourceBasePath", contractsSrcPath);

builder.Build().Run();
