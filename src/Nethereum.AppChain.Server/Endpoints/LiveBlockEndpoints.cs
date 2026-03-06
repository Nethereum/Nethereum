using System.Numerics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Storage;
using Nethereum.AppChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.AppChain.Server.Endpoints
{
    public static class LiveBlockEndpoints
    {
        public static void MapLiveBlockEndpoints(this WebApplication app)
        {
            app.MapGet("/blocks/latest", async (HttpContext context) =>
            {
                var blockStore = context.RequestServices.GetService<IBlockStore>();
                if (blockStore == null)
                {
                    return Results.NotFound(new { error = "Block store not available" });
                }

                var latestBlock = await blockStore.GetLatestAsync();
                if (latestBlock == null)
                {
                    return Results.NotFound(new { error = "No blocks found" });
                }

                var blockHash = await blockStore.GetHashByNumberAsync((long)latestBlock.BlockNumber);
                var finalityTracker = context.RequestServices.GetService<IFinalityTracker>();
                var isSoft = finalityTracker != null && await finalityTracker.IsSoftAsync(latestBlock.BlockNumber);

                return Results.Ok(FormatBlockHeader(latestBlock, blockHash, isSoft));
            });

            app.MapGet("/blocks/{blockNumber:long}", async (HttpContext context, long blockNumber) =>
            {
                var blockStore = context.RequestServices.GetService<IBlockStore>();
                if (blockStore == null)
                {
                    return Results.NotFound(new { error = "Block store not available" });
                }

                var block = await blockStore.GetByNumberAsync(blockNumber);
                if (block == null)
                {
                    return Results.NotFound(new { error = $"Block {blockNumber} not found" });
                }

                var blockHash = await blockStore.GetHashByNumberAsync(blockNumber);
                var finalityTracker = context.RequestServices.GetService<IFinalityTracker>();
                var isSoft = finalityTracker != null && await finalityTracker.IsSoftAsync(blockNumber);

                return Results.Ok(FormatBlockHeader(block, blockHash, isSoft));
            });

            app.MapGet("/blocks/{blockNumber:long}/full", async (HttpContext context, long blockNumber) =>
            {
                var blockStore = context.RequestServices.GetService<IBlockStore>();
                var txStore = context.RequestServices.GetService<ITransactionStore>();
                var receiptStore = context.RequestServices.GetService<IReceiptStore>();

                if (blockStore == null)
                {
                    return Results.NotFound(new { error = "Block store not available" });
                }

                var block = await blockStore.GetByNumberAsync(blockNumber);
                if (block == null)
                {
                    return Results.NotFound(new { error = $"Block {blockNumber} not found" });
                }

                var blockHash = await blockStore.GetHashByNumberAsync(blockNumber);
                var transactions = txStore != null ? await txStore.GetByBlockHashAsync(blockHash) : null;
                var receipts = receiptStore != null ? await receiptStore.GetByBlockNumberAsync(blockNumber) : null;

                var finalityTracker = context.RequestServices.GetService<IFinalityTracker>();
                var isSoft = finalityTracker != null && await finalityTracker.IsSoftAsync(blockNumber);

                return Results.Ok(new
                {
                    header = FormatBlockHeader(block, blockHash, isSoft),
                    transactions = transactions?.Select((tx, i) => FormatTransaction(tx, i, blockHash, blockNumber)),
                    receipts = receipts?.Select((r, i) => FormatReceipt(r, i))
                });
            });

            app.MapGet("/blocks/range", async (HttpContext context, long from, long to) =>
            {
                var blockStore = context.RequestServices.GetService<IBlockStore>();
                if (blockStore == null)
                {
                    return Results.NotFound(new { error = "Block store not available" });
                }

                if (to - from > 100)
                {
                    return Results.BadRequest(new { error = "Maximum 100 blocks per request" });
                }

                var finalityTracker = context.RequestServices.GetService<IFinalityTracker>();
                var blocks = new List<object>();

                for (long i = from; i <= to; i++)
                {
                    var block = await blockStore.GetByNumberAsync(i);
                    if (block == null)
                        break;

                    var blockHash = await blockStore.GetHashByNumberAsync(i);
                    var isSoft = finalityTracker != null && await finalityTracker.IsSoftAsync(i);
                    blocks.Add(FormatBlockHeader(block, blockHash, isSoft));
                }

                return Results.Ok(new
                {
                    from,
                    to = from + blocks.Count - 1,
                    count = blocks.Count,
                    blocks
                });
            });

            app.MapGet("/finality", async (HttpContext context) =>
            {
                var finalityTracker = context.RequestServices.GetService<IFinalityTracker>();
                var blockStore = context.RequestServices.GetService<IBlockStore>();
                var batchStore = context.RequestServices.GetService<IBatchStore>();

                var localTip = blockStore != null ? await blockStore.GetHeightAsync() : -1;
                var lastFinalized = finalityTracker != null ? await finalityTracker.GetLatestFinalizedBlockAsync() : -1;
                var lastSoft = finalityTracker != null ? await finalityTracker.GetLatestSoftBlockAsync() : -1;
                var lastBatchedBlock = batchStore != null ? await batchStore.GetLatestImportedBlockAsync() : -1;

                return Results.Ok(new
                {
                    localTip = localTip.ToString(),
                    lastFinalizedBlock = lastFinalized.ToString(),
                    lastSoftBlock = lastSoft.ToString(),
                    lastBatchedBlock = lastBatchedBlock.ToString(),
                    softBlockCount = lastSoft > lastFinalized ? (lastSoft - lastFinalized).ToString() : "0"
                });
            });

            app.MapGet("/finality/{blockNumber:long}", async (HttpContext context, long blockNumber) =>
            {
                var finalityTracker = context.RequestServices.GetService<IFinalityTracker>();
                if (finalityTracker == null)
                {
                    return Results.NotFound(new { error = "Finality tracker not available" });
                }

                var isFinalized = await finalityTracker.IsFinalizedAsync(blockNumber);
                var isSoft = await finalityTracker.IsSoftAsync(blockNumber);

                string status;
                if (isFinalized)
                    status = "finalized";
                else if (isSoft)
                    status = "soft";
                else
                    status = "unknown";

                return Results.Ok(new
                {
                    blockNumber = blockNumber.ToString(),
                    status,
                    isFinalized,
                    isSoft
                });
            });
        }

        private static object FormatBlockHeader(BlockHeader header, byte[]? blockHash, bool isSoft)
        {
            return new
            {
                number = header.BlockNumber.ToString(),
                hash = blockHash != null ? "0x" + Convert.ToHexString(blockHash).ToLowerInvariant() : null,
                parentHash = "0x" + Convert.ToHexString(header.ParentHash).ToLowerInvariant(),
                stateRoot = "0x" + Convert.ToHexString(header.StateRoot).ToLowerInvariant(),
                transactionsRoot = "0x" + Convert.ToHexString(header.TransactionsHash).ToLowerInvariant(),
                receiptsRoot = "0x" + Convert.ToHexString(header.ReceiptHash).ToLowerInvariant(),
                miner = header.Coinbase,
                gasLimit = header.GasLimit.ToString(),
                gasUsed = header.GasUsed.ToString(),
                timestamp = header.Timestamp.ToString(),
                baseFeePerGas = header.BaseFee.ToString(),
                finality = isSoft ? "soft" : "finalized"
            };
        }

        private static object FormatTransaction(ISignedTransaction tx, int index, byte[] blockHash, long blockNumber)
        {
            return new
            {
                hash = "0x" + Convert.ToHexString(tx.Hash).ToLowerInvariant(),
                blockHash = "0x" + Convert.ToHexString(blockHash).ToLowerInvariant(),
                blockNumber = blockNumber.ToString(),
                transactionIndex = index,
                type = tx.TransactionType.ToString(),
                rawEncoded = "0x" + Convert.ToHexString(tx.GetRLPEncoded()).ToLowerInvariant()
            };
        }

        private static object FormatReceipt(Receipt receipt, int index)
        {
            return new
            {
                transactionIndex = index,
                cumulativeGasUsed = receipt.CumulativeGasUsed.ToString(),
                logsCount = receipt.Logs?.Count ?? 0,
                bloomFilter = receipt.Bloom != null ? "0x" + Convert.ToHexString(receipt.Bloom).ToLowerInvariant() : null
            };
        }
    }
}
