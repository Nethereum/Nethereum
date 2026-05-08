using System.Text.Json;
using Nethereum.AppChain.Anchoring;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Anchoring");

var mainchainUrl = builder.Configuration["services:mainchain:http:0"]
    ?? builder.Configuration["Anchoring:MainChainUrl"]
    ?? "http://localhost:53500";

var appchainUrl = builder.Configuration["services:appchain:http:0"]
    ?? builder.Configuration["Anchoring:AppChainUrl"]
    ?? "http://localhost:53510";

var operatorKey = builder.Configuration["Anchoring:OperatorKey"]
    ?? "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

var cadence = int.TryParse(builder.Configuration["Anchoring:Cadence"], out var c) ? c : 10;
var intervalMs = int.TryParse(builder.Configuration["Anchoring:IntervalMs"], out var i) ? i : 30000;
var mainchainChainId = int.TryParse(builder.Configuration["Anchoring:MainChainId"], out var mc) ? mc : 1337;

string? anchorAddress = null;

app.MapGet("/", () => Results.Ok(new
{
    service = "Nethereum.AppChain.Anchoring",
    anchorContract = anchorAddress ?? "waiting for bootstrap",
    cadence,
    intervalMs
}));

_ = Task.Run(async () =>
{
    using var httpClient = new HttpClient();

    for (int attempt = 0; attempt < 30; attempt++)
    {
        try
        {
            await Task.Delay(3000);
            var json = await httpClient.GetStringAsync($"{mainchainUrl}/contracts");
            var contracts = JsonSerializer.Deserialize<JsonElement>(json);
            anchorAddress = contracts.GetProperty("anchor").GetString();
            if (!string.IsNullOrEmpty(anchorAddress)) break;
        }
        catch
        {
            logger.LogDebug("Waiting for mainchain contracts (attempt {Attempt})...", attempt + 1);
        }
    }

    if (string.IsNullOrEmpty(anchorAddress))
    {
        logger.LogError("Failed to get anchor contract address from mainchain");
        return;
    }

    logger.LogInformation("Anchor contract: {Address}, starting AnchorWorker", anchorAddress);

    var anchorConfig = new AnchorConfig
    {
        Enabled = true,
        AnchorCadence = cadence,
        AnchorIntervalMs = intervalMs,
        TargetRpcUrl = mainchainUrl,
        TargetChainId = mainchainChainId,
        AnchorContractAddress = anchorAddress,
        SequencerPrivateKey = operatorKey
    };

    var mainchainAccount = new Account(operatorKey, mainchainChainId);
    var mainchainWeb3 = new Web3(mainchainAccount, mainchainUrl);
    mainchainWeb3.TransactionManager.UseLegacyAsDefault = true;
    var anchorService = new EvmAnchorService(anchorConfig, mainchainWeb3,
        app.Services.GetService<ILogger<EvmAnchorService>>());

    var appchainWeb3 = new Web3(appchainUrl);
    var chainAnchorable = new RpcChainAnchorable(appchainWeb3);

    var worker = new AnchorWorker(
        chainAnchorable, anchorService, anchorConfig,
        logger: app.Services.GetService<ILogger<AnchorWorker>>());

    await worker.StartAsync(CancellationToken.None);
});

app.Run();
