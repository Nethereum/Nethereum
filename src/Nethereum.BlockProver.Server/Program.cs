using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockProver.Server;
using Nethereum.BlockProver.Server.Metrics;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBlockProverOptions(builder.Configuration);

var witnessStore = new InMemoryWitnessStore();
var requestQueue = new InMemoryProofRequestQueue();
var progressRepo = new InMemoryBlockchainProgressRepository();

var proverMode = builder.Configuration.GetValue<string>("ProverMode") ?? "Mock";
var proverEndpoint = builder.Configuration.GetValue<string>("ProverEndpoint");

IBlockProver prover = proverMode switch
{
    "Remote" when !string.IsNullOrEmpty(proverEndpoint) =>
        new HttpBlockProverClient(proverEndpoint, timeout: TimeSpan.FromMinutes(30)),
    _ => new MockBlockProver()
};

builder.Services.AddSingleton<IWitnessStore>(witnessStore);
builder.Services.AddSingleton<IProofRequestQueue>(requestQueue);
builder.Services.AddSingleton<IBlockProgressRepository>(progressRepo);
builder.Services.AddSingleton<IBlockProver>(prover);
builder.Services.AddSingleton<BlockProverMetrics>();
builder.Services.AddSingleton<BlockProverProcessingService>();
builder.Services.AddHostedService<BlockProverHostedService>();

var app = builder.Build();

var processingService = app.Services.GetRequiredService<BlockProverProcessingService>();
app.MapBlockProverEndpoints(requestQueue, witnessStore, processingService);

app.Run();
