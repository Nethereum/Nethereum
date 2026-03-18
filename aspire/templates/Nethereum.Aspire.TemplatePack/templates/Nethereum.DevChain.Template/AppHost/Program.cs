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

var explorer = builder.AddProject<Projects.Explorer>("explorer")
    .WithReference(postgres)
    .WithReference(devchain)
    .WaitFor(indexer)
    .WithEnvironment("Explorer__DevAccountPrivateKey", devAccountKey)
    .WithEnvironment("Explorer__EnablePendingTransactions", "true");

builder.Build().Run();
