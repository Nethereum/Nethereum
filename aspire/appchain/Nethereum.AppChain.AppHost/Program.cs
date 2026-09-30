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

// Shared genesis identity: the sequencer signs/owns with these keys; the follower derives the
// same addresses (so it produces an identical genesis block 0) but never signs.
const string sequencerKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
const string genesisOwnerKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

// Fixed DevP2P serve key → a STABLE enode for the sequencer, so the follower can predict the dial address.
const string devp2pNodeKey = "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a";
// enode NodeId = the uncompressed pubkey (no prefix) of devp2pNodeKey, i.e.
// new EthECKey(devp2pNodeKey).GetPubKeyNoPrefix().ToHex(). Hardcoded to keep the AppHost free of a crypto dep.
const string producerNodePubKey = "9d9031e97dd78ff8c15aa86939de9b1e791066a0224e331bc962a2099a7b1f0464b8bbafe1535f2301c72c2cb3535b172da30b02686ab0393d348614f157fbdb";

var appchain = builder.AddProject<Projects.Nethereum_AppChain_Node>("appchain")
    .WithReference(mainchain)
    .WithReference(appchainDb)
    .WaitFor(mainchain)
    .WaitFor(appchainDb)
    .WithEnvironment("AppChain__ChainId", "420420")
    .WithEnvironment("AppChain__SequencerPrivateKey", sequencerKey)
    .WithEnvironment("AppChain__GenesisOwnerPrivateKey", genesisOwnerKey)
    .WithEnvironment("AppChain__UseInMemoryStorage", "false")
    .WithEnvironment("AppChain__DatabasePath", "./appchain-data-sequencer")
    .WithEnvironment("AppChain__DeployMudWorld", "false")
    .WithEnvironment("AppChain__AllowEmptyBlocks", "true")
    .WithEnvironment("AppChain__BlockTimeMs", "2000")
    .WithEnvironment("AppChain__SyncMode", "None")
    // Serve eth/snap over DevP2P on a stable enode so the follower can snap-sync from us.
    .WithEnvironment("AppChain__EnableDevP2PServe", "true")
    .WithEnvironment("AppChain__DevP2PServePort", "30403")
    .WithEnvironment("AppChain__DevP2PNodeKey", devp2pNodeKey)
    .WithEndpoint(port: 30403, targetPort: 30403, name: "devp2p", scheme: "tcp", isProxied: false);

var devp2pEndpoint = appchain.GetEndpoint("devp2p");

// The follower: same chain + genesis keys (so it builds an identical genesis block 0), RocksDB. Cold-syncs
// from the sequencer over DevP2P eth/snap (state via snap, then forward-execute) instead of the HTTP source.
var appchainFollower = builder.AddProject<Projects.Nethereum_AppChain_Node>("appchain-follower")
    .WithReference(appchain)
    .WaitFor(appchain)
    .WithEnvironment("AppChain__ChainId", "420420")
    .WithEnvironment("AppChain__SequencerPrivateKey", sequencerKey)
    .WithEnvironment("AppChain__GenesisOwnerPrivateKey", genesisOwnerKey)
    .WithEnvironment("AppChain__UseInMemoryStorage", "false")
    .WithEnvironment("AppChain__DatabasePath", "./appchain-data-follower")
    .WithEnvironment("AppChain__DeployMudWorld", "false")
    .WithEnvironment("AppChain__DevP2PProducerEnode",
        ReferenceExpression.Create($"enode://{producerNodePubKey}@{devp2pEndpoint.Property(EndpointProperty.Host)}:{devp2pEndpoint.Property(EndpointProperty.Port)}"));

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

var anchoringIndexer = builder.AddProject<Projects.Nethereum_AppChain_AnchoringIndexer>("anchoring-indexer")
    .WithReference(mainchain)
    .WithReference(mainchainDb)
    .WaitFor(mainchainIndexer)
    .WaitFor(mainchainDb);

var mainchainExplorer = builder.AddProject<Projects.Nethereum_AppChain_MainChain_Explorer>("mainchain-explorer")
    .WithReference(mainchain)
    .WithReference(mainchainDb)
    .WithReference(anchoring)
    .WaitFor(mainchainIndexer)
    .WaitFor(anchoringIndexer);

var appchainIndexer = builder.AddProject<Projects.Nethereum_AppChain_Indexer>("appchain-indexer")
    .WithReference(appchain)
    .WithReference(appchainDb)
    .WaitFor(appchain)
    .WaitFor(appchainDb);

var appchainExplorer = builder.AddProject<Projects.Nethereum_AppChain_Explorer>("appchain-explorer")
    .WithReference(appchain)
    .WithReference(appchainDb)
    .WithReference(mainchainDb)
    .WithReference(anchoring)
    .WaitFor(appchainIndexer)
    .WaitFor(anchoringIndexer);

builder.Build().Run();
