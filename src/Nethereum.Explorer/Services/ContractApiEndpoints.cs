using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.ABIDeserialisation;

namespace Nethereum.Explorer.Services;

public static class ContractApiEndpoints
{
    private const int MaxBatchSize = 50;

    public static WebApplication MapContractApiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/contracts").RequireRateLimiting("api");

        group.MapGet("/{address}/abi", async (string address, IAbiStorageService abiStorage) =>
        {
            var abiInfo = await abiStorage.GetContractAbiAsync(address);
            if (abiInfo == null)
                return Results.NotFound(new { error = "ABI not found for this contract" });

            return Results.Ok(new
            {
                address = address.ToLowerInvariant(),
                name = abiInfo.ContractName,
                abi = abiInfo.ABI,
                hasMetadata = abiInfo.Metadata != null
            });
        });

        group.MapPost("/{address}/abi", async (string address, AbiUploadRequest request,
            IAbiStorageService abiStorage, IOptions<ExplorerOptions> options,
            HttpContext httpContext, ILogger<AbiUploadRequest> logger) =>
        {
            if (!ValidateApiKey(httpContext, options.Value))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Abi))
                return Results.BadRequest(new { error = "ABI JSON is required" });

            try
            {
                var contractAbi = ABIDeserialiserFactory.DeserialiseContractABI(request.Abi);
                if (contractAbi == null)
                    return Results.BadRequest(new { error = "Invalid ABI JSON" });
            }
            catch
            {
                return Results.BadRequest(new { error = "Failed to parse ABI" });
            }

            await abiStorage.StoreAbiAsync(address, request.Abi, request.Name, AbiSource.LocalUpload);
            logger.LogInformation("ABI registered via API for contract {Address}", address.ToLowerInvariant());

            return Results.Ok(new
            {
                address = address.ToLowerInvariant(),
                name = request.Name,
                status = "stored"
            });
        });

        group.MapPost("/batch", async (BatchAbiUploadRequest request,
            IAbiStorageService abiStorage, IOptions<ExplorerOptions> options,
            HttpContext httpContext, ILogger<BatchAbiUploadRequest> logger) =>
        {
            if (!ValidateApiKey(httpContext, options.Value))
                return Results.Unauthorized();

            if (request.Contracts == null || !request.Contracts.Any())
                return Results.BadRequest(new { error = "At least one contract entry is required" });

            if (request.Contracts.Count > MaxBatchSize)
                return Results.BadRequest(new { error = $"Batch size exceeds maximum of {MaxBatchSize}" });

            var results = new List<object>();
            foreach (var entry in request.Contracts)
            {
                if (string.IsNullOrWhiteSpace(entry.Address) || string.IsNullOrWhiteSpace(entry.Abi))
                {
                    results.Add(new { address = entry.Address, status = "skipped", error = "Address and ABI are required" });
                    continue;
                }

                try
                {
                    var contractAbi = ABIDeserialiserFactory.DeserialiseContractABI(entry.Abi);
                    if (contractAbi == null)
                    {
                        results.Add(new { address = entry.Address, status = "skipped", error = "Invalid ABI" });
                        continue;
                    }

                    await abiStorage.StoreAbiAsync(entry.Address, entry.Abi, entry.Name, AbiSource.LocalUpload);
                    results.Add(new { address = entry.Address.ToLowerInvariant(), status = "stored" });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to process ABI for {Address}", entry.Address);
                    results.Add(new { address = entry.Address, status = "error", error = "Failed to process ABI" });
                }
            }

            logger.LogInformation("Batch ABI registration: {Count} contracts processed", request.Contracts.Count);
            return Results.Ok(new { processed = results.Count, results });
        });

        return app;
    }

    private static bool ValidateApiKey(HttpContext httpContext, ExplorerOptions options)
    {
        if (string.IsNullOrEmpty(options.ApiKey))
            return true;

        var providedKey = httpContext.Request.Headers["X-Api-Key"].FirstOrDefault();
        return string.Equals(providedKey, options.ApiKey, StringComparison.Ordinal);
    }
}

public class AbiUploadRequest
{
    public string Abi { get; set; } = "";
    public string? Name { get; set; }
}

public class BatchAbiUploadRequest
{
    public List<BatchAbiEntry> Contracts { get; set; } = new();
}

public class BatchAbiEntry
{
    public string Address { get; set; } = "";
    public string Abi { get; set; } = "";
    public string? Name { get; set; }
}
