var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres")
    .WithDataVolume("nethereum-pgdata");

var postgres = postgresServer.AddDatabase("nethereumdb");

var devchain = builder.AddProject<Projects.DevChain>("devchain");

var indexer = builder.AddProject<Projects.Indexer>("indexer")
    .WithReference(postgres)
    .WithReference(devchain)
    .WaitFor(devchain)
    .WaitFor(postgres);

var devAccountKey = builder.AddParameter("devAccountPrivateKey", secret: true);

var contractsOutPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "contracts", "out"));
var contractsSrcPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "contracts"));

var explorer = builder.AddProject<Projects.Explorer>("explorer")
    .WithReference(postgres)
    .WithReference(devchain)
    .WaitFor(indexer)
    .WithEnvironment("Explorer__DevAccountPrivateKey", devAccountKey)
    .WithEnvironment("Explorer__EnablePendingTransactions", "true")
    .WithEnvironment("Explorer__AbiSources__LocalStorageEnabled", "true")
    .WithEnvironment("Explorer__AbiSources__LocalStoragePath", contractsOutPath)
    .WithEnvironment("Explorer__AbiSources__SourceBasePath", contractsSrcPath);

var webapp = builder.AddProject<Projects.WebApp>("webapp")
    .WithReference(devchain)
    .WaitFor(devchain);

var loadgenerator = builder.AddProject<Projects.LoadGenerator>("loadgenerator")
    .WithReference(devchain)
    .WaitFor(devchain);

builder.Build().Run();
