using System.Text.Json;
using Nethereum.CoreChain.Proving;
using Nethereum.Zisk.Prover.Server;

var builder = WebApplication.CreateBuilder(args);

var mode = builder.Configuration.GetValue<string>("ProverMode") ?? "Mock";
var elfPath = builder.Configuration.GetValue<string>("ElfPath");

var proverCommand = builder.Configuration.GetValue<string>("ProverCommand") ?? "ziskemu";
var proverArgs = builder.Configuration.GetValue<string>("ProverArgs")
    ?? "-e {elf} --legacy-inputs {witness} -n 100000000";

IBlockProver prover = mode switch
{
    "Emulate" or "Prove" => new ZiskEmuBlockProver(
        elfPath ?? throw new InvalidOperationException(
            "ElfPath required for Emulate/Prove mode. Set via --ElfPath or appsettings.json"),
        command: proverCommand,
        argsTemplate: proverArgs),
    _ => new MockBlockProver()
};

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Nethereum.Zisk.Prover.Server", mode }));

app.MapPost("/prove", async (HttpContext ctx) =>
{
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    var body = ms.ToArray();

    if (body.Length == 0)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("{\"error\":\"Empty request body\"}");
        return;
    }

    var request = JsonSerializer.Deserialize<ProveBlockRequest>(body);
    if (request == null || request.WitnessBytes == null)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("{\"error\":\"Invalid request\"}");
        return;
    }

    var witnessBytes = Convert.FromBase64String(request.WitnessBytes);
    var preStateRoot = request.PreStateRoot != null ? Convert.FromBase64String(request.PreStateRoot) : null;
    var postStateRoot = request.PostStateRoot != null ? Convert.FromBase64String(request.PostStateRoot) : null;

    var result = await prover.ProveBlockAsync(witnessBytes, preStateRoot, postStateRoot, request.BlockNumber);

    var response = new ProveBlockResponse
    {
        ProofBytes = Convert.ToBase64String(result.ProofBytes ?? Array.Empty<byte>()),
        PreStateRoot = Convert.ToBase64String(result.PreStateRoot ?? Array.Empty<byte>()),
        PostStateRoot = Convert.ToBase64String(result.PostStateRoot ?? Array.Empty<byte>()),
        WitnessHash = Convert.ToBase64String(result.WitnessHash ?? Array.Empty<byte>()),
        BlockNumber = result.BlockNumber,
        ProverMode = result.ProverMode
    };

    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(response));
});

app.Run();
