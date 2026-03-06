using System.IO;
using System.Numerics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sync;

namespace Nethereum.AppChain.Server.Endpoints
{
    public static class BatchSyncEndpoints
    {
        public static void MapBatchSyncEndpoints(this WebApplication app)
        {
            var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger("BatchSync");

            app.MapGet("/batches/{fileName}", async (HttpContext context, string fileName) =>
            {
                var config = context.RequestServices.GetService<Configuration.AppChainServerConfig>();
                if (config?.BatchOutputDirectory == null)
                {
                    return Results.NotFound(new { error = "Batch serving not configured" });
                }

                var sanitizedFileName = Path.GetFileName(fileName);
                if (string.IsNullOrEmpty(sanitizedFileName) || sanitizedFileName != fileName)
                {
                    return Results.BadRequest(new { error = "Invalid file name" });
                }

                var filePath = Path.Combine(config.BatchOutputDirectory, sanitizedFileName);
                if (!File.Exists(filePath))
                {
                    return Results.NotFound(new { error = "Batch not found" });
                }

                var contentType = sanitizedFileName.EndsWith(".zst") ? "application/zstd" : "application/octet-stream";
                return Results.File(filePath, contentType, sanitizedFileName);
            });

            app.MapGet("/batches", async (HttpContext context, long? fromBlock, long? toBlock, int? limit) =>
            {
                var batchStore = context.RequestServices.GetService<IBatchStore>();
                if (batchStore == null)
                {
                    return Results.NotFound(new { error = "Batch store not available" });
                }

                var batches = await batchStore.GetBatchesAfterAsync(fromBlock ?? 0, limit ?? 100);
                return Results.Ok(new
                {
                    count = batches.Count,
                    batches = batches.Select(b => new
                    {
                        batchId = b.BatchId,
                        fromBlock = b.FromBlock.ToString(),
                        toBlock = b.ToBlock.ToString(),
                        blockCount = b.BlockCount,
                        status = b.Status.ToString(),
                        batchHash = Convert.ToHexString(b.BatchHash).ToLowerInvariant()
                    })
                });
            });

            app.MapGet("/batches/latest", async (HttpContext context) =>
            {
                var batchStore = context.RequestServices.GetService<IBatchStore>();
                if (batchStore == null)
                {
                    return Results.NotFound(new { error = "Batch store not available" });
                }

                var batch = await batchStore.GetLatestBatchAsync();
                if (batch == null)
                {
                    return Results.NotFound(new { error = "No batches available" });
                }

                return Results.Ok(new
                {
                    batchId = batch.BatchId,
                    fromBlock = batch.FromBlock.ToString(),
                    toBlock = batch.ToBlock.ToString(),
                    blockCount = batch.BlockCount,
                    status = batch.Status.ToString(),
                    batchHash = Convert.ToHexString(batch.BatchHash).ToLowerInvariant()
                });
            });

            app.MapGet("/snapshots/{fileName}", async (HttpContext context, string fileName) =>
            {
                var config = context.RequestServices.GetService<Configuration.AppChainServerConfig>();
                if (config?.SnapshotOutputDirectory == null)
                {
                    return Results.NotFound(new { error = "Snapshot serving not configured" });
                }

                var sanitizedFileName = Path.GetFileName(fileName);
                if (string.IsNullOrEmpty(sanitizedFileName) || sanitizedFileName != fileName)
                {
                    return Results.BadRequest(new { error = "Invalid file name" });
                }

                var filePath = Path.Combine(config.SnapshotOutputDirectory, sanitizedFileName);
                if (!File.Exists(filePath))
                {
                    return Results.NotFound(new { error = "Snapshot not found" });
                }

                var contentType = sanitizedFileName.EndsWith(".zst") ? "application/zstd" : "application/octet-stream";
                return Results.File(filePath, contentType, sanitizedFileName);
            });

            app.MapGet("/sync/status", async (HttpContext context) =>
            {
                var syncService = context.RequestServices.GetService<IBatchSyncService>();
                if (syncService == null)
                {
                    return Results.Ok(new
                    {
                        available = false,
                        message = "Batch sync service not available (this is a sequencer node)"
                    });
                }

                return Results.Ok(new
                {
                    available = true,
                    state = syncService.State.ToString(),
                    localTip = syncService.LocalTip.ToString(),
                    anchoredTip = syncService.AnchoredTip.ToString()
                });
            });
        }
    }
}
