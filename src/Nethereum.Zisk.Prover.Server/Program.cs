using System.Text.Json;
using Nethereum.CoreChain.Proving;

var builder = WebApplication.CreateBuilder(args);

var mode = builder.Configuration.GetValue<string>("ProverMode") ?? "Mock";
IBlockProver prover = mode switch
{
    "Emulate" => throw new NotImplementedException("Emulate mode requires ziskemu — coming soon"),
    "Prove" => throw new NotImplementedException("Prove mode requires cargo-zisk — coming soon"),
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
