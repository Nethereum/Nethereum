using System.Text.Json;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Anchoring.Strategies;
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
var daStr = builder.Configuration["Anchoring:DataAvailability"] ?? "None";
var proofStr = builder.Configuration["Anchoring:ProofMode"] ?? "None";
var da = Enum.TryParse<AnchoringDataAvailability>(daStr, true, out var daVal)
    ? daVal : AnchoringDataAvailability.None;
var proofMode = Enum.TryParse<AnchoringProofMode>(proofStr, true, out var pmVal)
    ? pmVal : AnchoringProofMode.None;

string? anchorAddress = null;
AnchorWorker? worker = null;
AnchorConfig? anchorConfig = null;
IAnchorSubmissionStrategy? currentStrategy = null;
IChainAnchorable? chainAnchorable = null;
IAnchorService? anchorSvc = null;
var configLock = new SemaphoreSlim(1, 1);

app.MapGet("/", () => Results.Ok(new
{
    service = "Nethereum.AppChain.Anchoring",
    anchorContract = anchorAddress ?? "waiting for bootstrap",
    cadence = anchorConfig?.AnchorCadence ?? cadence,
    intervalMs = anchorConfig?.AnchorIntervalMs ?? intervalMs,
    dataAvailability = (anchorConfig?.DataAvailability ?? da).ToString(),
    proofMode = (anchorConfig?.ProofMode ?? proofMode).ToString()
}));

app.MapGet("/api/anchor/status", () =>
{
    if (worker == null || anchorConfig == null)
        return Results.Ok(new { status = "initializing" });

    return Results.Ok(new
    {
        status = worker.IsRunning ? "running" : "stopped",
        lastAnchoredBlock = worker.LastAnchoredBlock.ToString(),
        strategy = currentStrategy?.Name ?? "unknown",
        dataAvailability = anchorConfig.DataAvailability.ToString(),
        proofMode = anchorConfig.ProofMode.ToString(),
        cadence = anchorConfig.AnchorCadence,
        intervalMs = anchorConfig.AnchorIntervalMs,
        enabled = anchorConfig.Enabled,
        anchorContract = anchorAddress
    });
});

app.MapGet("/api/anchor/strategies", () =>
{
    var strategies = new[]
    {
        new { da = "None", proof = "None", name = "CommitmentOnly" },
        new { da = "Calldata", proof = "None", name = "Calldata_SyncOnly" },
        new { da = "None", proof = "StarkHash", name = "NoDA_StarkHash_OffChainVerifiable" },
        new { da = "Calldata", proof = "StarkHash", name = "Calldata_StarkHash_SyncAndOffChainVerifiable" },
        new { da = "None", proof = "SnarkOnChain", name = "NoDA_SnarkOnChain_TrustlessVerification" },
        new { da = "Calldata", proof = "SnarkOnChain", name = "Calldata_SnarkOnChain_SyncAndTrustlessVerification" },
        new { da = "BlobReference", proof = "SnarkOnChain", name = "BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA" },
    };
    return Results.Ok(strategies);
});

app.MapPost("/api/anchor/config", async (HttpRequest request) =>
{
    if (worker == null || anchorConfig == null)
        return Results.BadRequest(new { error = "Worker not initialized" });

    await configLock.WaitAsync();
    try
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);

        if (body.TryGetProperty("cadence", out var cadenceProp) && cadenceProp.TryGetInt32(out var newCadence))
        {
            if (newCadence < 1) return Results.BadRequest(new { error = "Cadence must be >= 1" });
            anchorConfig.AnchorCadence = newCadence;
        }

        if (body.TryGetProperty("intervalMs", out var intervalProp) && intervalProp.TryGetInt32(out var newInterval))
        {
            if (newInterval < 1000) return Results.BadRequest(new { error = "IntervalMs must be >= 1000" });
            anchorConfig.AnchorIntervalMs = newInterval;
        }

        if (body.TryGetProperty("enabled", out var enabledProp))
            anchorConfig.Enabled = enabledProp.GetBoolean();

        if (body.TryGetProperty("dataAvailability", out var daProp) &&
            Enum.TryParse<AnchoringDataAvailability>(daProp.GetString(), true, out var newDa) &&
            body.TryGetProperty("proofMode", out var pmProp) &&
            Enum.TryParse<AnchoringProofMode>(pmProp.GetString(), true, out var newPm))
        {
            anchorConfig.DataAvailability = newDa;
            anchorConfig.ProofMode = newPm;

            var newStrategy = AnchoringStrategyFactory.Create(newDa, newPm);
            currentStrategy = newStrategy;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await worker.StopAsync(cts.Token);
            worker.Dispose();

            worker = new AnchorWorker(
                chainAnchorable!, anchorSvc!, anchorConfig,
                strategy: newStrategy,
                logger: app.Services.GetService<ILogger<AnchorWorker>>());

            await worker.StartAsync(CancellationToken.None);

            logger.LogInformation("Strategy switched to {Strategy} (DA={DA}, Proof={Proof})",
                newStrategy.Name, newDa, newPm);
        }

        return Results.Ok(new
        {
            status = "updated",
            strategy = currentStrategy?.Name,
            dataAvailability = anchorConfig.DataAvailability.ToString(),
            proofMode = anchorConfig.ProofMode.ToString(),
            cadence = anchorConfig.AnchorCadence,
            intervalMs = anchorConfig.AnchorIntervalMs,
            enabled = anchorConfig.Enabled
        });
    }
    finally
    {
        configLock.Release();
    }
});

app.MapPost("/api/anchor/force", async () =>
{
    if (worker == null)
        return Results.BadRequest(new { error = "Worker not initialized" });

    try
    {
        await worker.ForceAnchorAsync();
        return Results.Ok(new
        {
            status = "anchored",
            lastAnchoredBlock = worker.LastAnchoredBlock.ToString()
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

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

    anchorConfig = new AnchorConfig
    {
        Enabled = true,
        AnchorCadence = cadence,
        AnchorIntervalMs = intervalMs,
        TargetRpcUrl = mainchainUrl,
        TargetChainId = mainchainChainId,
        AnchorContractAddress = anchorAddress,
        SequencerPrivateKey = operatorKey,
        DataAvailability = da,
        ProofMode = proofMode
    };

    var appChainId = ulong.TryParse(builder.Configuration["Anchoring:AppChainId"], out var acid) ? acid : 420420UL;
    var genesisHash = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes($"appchain-{appChainId}"));

    var mainchainAccount = new Account(operatorKey, mainchainChainId);
    var mainchainWeb3 = new Web3(mainchainAccount, mainchainUrl);
    mainchainWeb3.TransactionManager.UseLegacyAsDefault = true;
    anchorSvc = new AppChainAnchorBatchService(anchorConfig, mainchainWeb3,
        appChainId, genesisHash,
        app.Services.GetService<ILogger<AppChainAnchorBatchService>>());

    var appchainWeb3 = new Web3(appchainUrl);
    chainAnchorable = new RpcChainAnchorable(appchainWeb3);

    currentStrategy = AnchoringStrategyFactory.Create(da, proofMode);
    logger.LogInformation("Anchoring strategy: {Strategy} (DA={DA}, Proof={Proof})", currentStrategy.Name, da, proofMode);

    worker = new AnchorWorker(
        chainAnchorable, anchorSvc, anchorConfig,
        strategy: currentStrategy,
        logger: app.Services.GetService<ILogger<AnchorWorker>>());

    await worker.StartAsync(CancellationToken.None);
});

app.Run();
