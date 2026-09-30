using System;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;

namespace Nethereum.BlockProver.Server
{
    public static class BlockProverEndpoints
    {
        public static IEndpointRouteBuilder MapBlockProverEndpoints(
            this IEndpointRouteBuilder app,
            IProofRequestQueue requestQueue,
            IWitnessStore witnessStore,
            BlockProverProcessingService processingService)
        {
            app.MapGet("/status", () => Results.Ok(new
            {
                service = "Nethereum.BlockProver.Server",
                lastProvenBlock = processingService.LastProvenBlock
            }));

            app.MapPost("/witness/{blockNumber}", async (long blockNumber, HttpContext ctx) =>
            {
                using var ms = new System.IO.MemoryStream();
                await ctx.Request.Body.CopyToAsync(ms);
                var witnessBytes = ms.ToArray();

                if (witnessBytes.Length == 0)
                    return Results.BadRequest(new { error = "Empty witness" });

                await witnessStore.StoreWitnessAsync(blockNumber, witnessBytes);
                return Results.Ok(new { blockNumber, witnessSize = witnessBytes.Length });
            });

            app.MapPost("/queue/{blockNumber}", async (long blockNumber) =>
            {
                await requestQueue.EnqueueAsync(blockNumber);
                return Results.Accepted($"/queue/{blockNumber}", new { blockNumber, status = "Queued" });
            });

            app.MapPost("/queue/{blockNumber}/retry", async (long blockNumber) =>
            {
                await requestQueue.EnqueueAsync(blockNumber);
                return Results.Ok(new { blockNumber, status = "Requeued" });
            });

            app.MapGet("/queue/{blockNumber}", async (long blockNumber) =>
            {
                var status = await requestQueue.GetStatusAsync(blockNumber);
                if (status == null)
                    return Results.NotFound(new { blockNumber, error = "No request found" });
                return Results.Ok(new
                {
                    blockNumber = status.BlockNumber,
                    status = status.Status.ToString(),
                    attempts = status.Attempts,
                    lastError = status.LastError
                });
            });

            app.MapGet("/queue", async () =>
            {
                var pending = await requestQueue.GetPendingAsync();
                return Results.Ok(pending);
            });

            app.MapGet("/proof/{blockNumber}", async (long blockNumber) =>
            {
                var proof = await witnessStore.GetProofAsync(blockNumber);
                if (proof == null)
                    return Results.NotFound(new { blockNumber, error = "No proof found" });
                return Results.Ok(new
                {
                    blockNumber = proof.BlockNumber,
                    proofSize = proof.ProofBytes?.Length ?? 0,
                    proverMode = proof.ProverMode,
                    elfHash = proof.ElfHash != null ? Convert.ToHexString(proof.ElfHash) : null,
                    hasWitnessHash = proof.WitnessHash != null
                });
            });

            app.MapGet("/witness/{blockNumber}", async (long blockNumber) =>
            {
                var witness = await witnessStore.GetWitnessAsync(blockNumber);
                if (witness == null)
                    return Results.NotFound(new { blockNumber, error = "No witness found" });
                return Results.Ok(new { blockNumber, witnessSize = witness.Length });
            });

            return app;
        }
    }
}
