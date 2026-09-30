using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Proving;
using Nethereum.Zisk.Prover.Server;

var builder = WebApplication.CreateBuilder(args);

var mode = builder.Configuration.GetValue<string>("ProverMode") ?? "Mock";
var elfPath = builder.Configuration.GetValue<string>("ElfPath");
var proverCommand = builder.Configuration.GetValue<string>("ProverCommand") ?? "ziskemu";
var proverArgs = builder.Configuration.GetValue<string>("ProverArgs")
    ?? "-e {elf} --legacy-inputs {witness} -n 100000000";
var convertPathsForWsl = builder.Configuration.GetValue<bool>("ConvertPathsForWsl");
var provingKeySnarkPath = builder.Configuration.GetValue<string>("ProvingKeySnarkPath");
var useWsl = builder.Configuration.GetValue<bool>("UseWsl");
var timeoutMs = builder.Configuration.GetValue<int?>("TimeoutMs") ?? 1800000;

var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
var proverLogger = loggerFactory.CreateLogger<ZiskProveBlockProver>();

IBlockProver prover;
try
{
    prover = mode switch
    {
        "Emulate" => new ZiskEmuBlockProver(
            elfPath ?? throw new InvalidOperationException("ElfPath required for Emulate mode"),
            command: proverCommand,
            argsTemplate: proverArgs,
            convertPathsForWsl: convertPathsForWsl),
        "Prove" => new ZiskProveBlockProver(
            elfPath ?? throw new InvalidOperationException("ElfPath required for Prove mode"),
            cargoZiskPath: proverCommand.Contains("cargo-zisk") ? proverCommand : "cargo-zisk",
            provingKeySnarkPath: provingKeySnarkPath,
            convertPathsForWsl: convertPathsForWsl,
            useWsl: useWsl,
            timeoutMs: timeoutMs,
            logger: proverLogger,
            environmentVariables: new Dictionary<string, string>
            {
                ["HWLOC_COMPONENTS"] = "-gl"
            }),
        _ => new MockBlockProver()
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL: Failed to initialize prover (mode={mode}): {ex.Message}");
    return;
}

Console.WriteLine($"Nethereum.Zisk.Prover.Server starting: mode={mode}, elf={elfPath ?? "none"}");

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.MapGet("/", () => Results.Ok(new
{
    service = "Nethereum.Zisk.Prover.Server",
    mode,
    elfPath = elfPath ?? "none",
    proverCommand
}));

app.MapPost("/prove", async (HttpContext ctx) =>
{
    var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
    logger.LogInformation("[{RequestId}] /prove request received, content-length={Length}",
        requestId, ctx.Request.ContentLength);

    try
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();

        if (body.Length == 0)
        {
            logger.LogWarning("[{RequestId}] Empty request body", requestId);
            return Results.BadRequest(new { error = "Empty request body", requestId });
        }

        var request = JsonSerializer.Deserialize<ProveBlockRequest>(body);
        if (request == null || request.WitnessBytes == null)
        {
            logger.LogWarning("[{RequestId}] Invalid request: null or missing WitnessBytes", requestId);
            return Results.BadRequest(new { error = "Invalid request — WitnessBytes required", requestId });
        }

        var witnessBytes = Convert.FromBase64String(request.WitnessBytes);
        var preStateRoot = request.PreStateRoot != null ? Convert.FromBase64String(request.PreStateRoot) : null;
        var postStateRoot = request.PostStateRoot != null ? Convert.FromBase64String(request.PostStateRoot) : null;

        logger.LogInformation("[{RequestId}] Proving block {BlockNumber}: witness={WitnessSize} bytes",
            requestId, request.BlockNumber, witnessBytes.Length);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await prover.ProveBlockAsync(witnessBytes, preStateRoot, postStateRoot, request.BlockNumber);
        sw.Stop();

        logger.LogInformation("[{RequestId}] Block {BlockNumber} proof complete in {Duration:F1}s: mode={Mode}, proofSize={Size}, stateRootVerified={Verified}",
            requestId, result.BlockNumber, sw.Elapsed.TotalSeconds,
            result.ProverMode, result.ProofBytes?.Length ?? 0, result.StateRootVerified);

        var response = new ProveBlockResponse
        {
            ProofBytes = Convert.ToBase64String(result.ProofBytes ?? Array.Empty<byte>()),
            PreStateRoot = Convert.ToBase64String(result.PreStateRoot ?? Array.Empty<byte>()),
            PostStateRoot = Convert.ToBase64String(result.PostStateRoot ?? Array.Empty<byte>()),
            WitnessHash = Convert.ToBase64String(result.WitnessHash ?? Array.Empty<byte>()),
            ElfHash = result.ElfHash != null ? Convert.ToBase64String(result.ElfHash) : null,
            ProverComputedStateRoot = result.ProverComputedStateRoot != null
                ? Convert.ToBase64String(result.ProverComputedStateRoot) : null,
            ProverComputedBlockHash = result.ProverComputedBlockHash != null
                ? Convert.ToBase64String(result.ProverComputedBlockHash) : null,
            StateRootVerified = result.StateRootVerified,
            BlockHashVerified = result.BlockHashVerified,
            GasUsed = result.GasUsed,
            BlockNumber = result.BlockNumber,
            ProverMode = result.ProverMode
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[{RequestId}] Unhandled exception in /prove", requestId);
        return Results.Problem($"Internal error: {ex.Message}", statusCode: 500);
    }
});

app.Run();
