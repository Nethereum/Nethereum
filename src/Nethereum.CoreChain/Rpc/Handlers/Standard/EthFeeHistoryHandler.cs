using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.RLP;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthFeeHistoryHandler : RpcHandlerBase
    {
        public const int MaxRewardPercentiles = 100;

        public override string MethodName => ApiMethods.eth_feeHistory.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                return await HandleCoreAsync(request, context);
            }
            catch (RpcException)
            {
                throw;
            }
            catch
            {
                return BuildDefaultResponse(request, context.Node.Config.BaseFee);
            }
        }

        private static void ValidateRewardPercentiles(List<double> percentiles)
        {
            if (percentiles == null) return;

            if (percentiles.Count > MaxRewardPercentiles)
            {
                throw RpcException.InvalidParams($"invalid reward percentile: over the query limit {MaxRewardPercentiles}");
            }

            for (var i = 0; i < percentiles.Count; i++)
            {
                var percentile = percentiles[i];
                if (double.IsNaN(percentile) || percentile < 0 || percentile > 100)
                {
                    throw RpcException.InvalidParams($"invalid reward percentile: {percentile}");
                }

                if (i > 0 && percentile <= percentiles[i - 1])
                {
                    throw RpcException.InvalidParams(
                        $"invalid reward percentile: #{i - 1}:{percentiles[i - 1]} >= #{i}:{percentile}");
                }
            }
        }

        private RpcResponseMessage BuildDefaultResponse(RpcRequestMessage request, BigInteger baseFee)
        {
            var baseFeeHex = new HexBigInteger(baseFee);
            var zeroBlobFee = new HexBigInteger(0);
            var result = new FeeHistoryResult
            {
                OldestBlock = new HexBigInteger(0),
                BaseFeePerGas = new[] { baseFeeHex, baseFeeHex },
                GasUsedRatio = new[] { 0m },
                Reward = new[] { new[] { new HexBigInteger(0) } },
                BaseFeePerBlobGas = new[] { zeroBlobFee, zeroBlobFee },
                BlobGasUsedRatio = new[] { 0m }
            };
            return Success(request.Id, result);
        }

        private async Task<RpcResponseMessage> HandleCoreAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockCountParam = GetParam<object>(request, 0);
            var newestBlockTag = GetParam<string>(request, 1);
            var rewardPercentiles = GetOptionalParam<List<double>>(request, 2, null);
            ValidateRewardPercentiles(rewardPercentiles);

            if (!TryParseBlockCount(blockCountParam, out var blockCount))
                return BuildDefaultResponse(request, context.Node.Config.BaseFee);

            blockCount = Math.Min(blockCount, 1024);

            var newestBlock = await ResolveNewestBlockAsync(newestBlockTag, context);
            if (newestBlock < 0) newestBlock = 0;

            var baseFeePerGas = new List<HexBigInteger>();
            var gasUsedRatio = new List<decimal>();
            var baseFeePerBlobGas = new List<HexBigInteger>();
            var blobGasUsedRatio = new List<decimal>();
            var reward = rewardPercentiles != null ? new List<HexBigInteger[]>() : null;

            var oldestBlock = BigInteger.Max(0, newestBlock - blockCount + 1);
            var newestHeader = await CollectRowsAsync(
                context, baseFeePerGas, gasUsedRatio, baseFeePerBlobGas, blobGasUsedRatio, reward, rewardPercentiles,
                oldestBlock, newestBlock);

            if (baseFeePerGas.Count == 0)
                AddDefaultBlockEntry(context, baseFeePerGas, gasUsedRatio, baseFeePerBlobGas, blobGasUsedRatio, reward, rewardPercentiles);

            baseFeePerGas.Add(new HexBigInteger(ProjectedNextBaseFee(context, newestHeader)));
            baseFeePerBlobGas.Add(ProjectedNextBlobBaseFee(context, newestHeader));

            var result = new FeeHistoryResult
            {
                OldestBlock = new HexBigInteger(oldestBlock),
                BaseFeePerGas = baseFeePerGas.ToArray(),
                GasUsedRatio = gasUsedRatio.ToArray(),
                Reward = reward?.ToArray(),
                BaseFeePerBlobGas = baseFeePerBlobGas.ToArray(),
                BlobGasUsedRatio = blobGasUsedRatio.ToArray()
            };

            return Success(request.Id, result);
        }

        private static bool TryParseBlockCount(object blockCountParam, out int blockCount)
        {
            blockCount = 0;
            if (blockCountParam is string hexStr)
            {
                blockCount = (int)hexStr.HexToBigInteger(false);
            }
            else if (blockCountParam is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    blockCount = (int)jsonElement.RequireRpcString("blockCount").HexToBigInteger(false);
                }
                else if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    blockCount = jsonElement.GetInt32();
                }
                else
                {
                    return false;
                }
            }
            else if (blockCountParam is HexBigInteger hexBigInt)
            {
                blockCount = hexBigInt.Value > int.MaxValue ? int.MaxValue : (int)hexBigInt.Value;
            }
            else if (blockCountParam is BigInteger bigInt)
            {
                blockCount = bigInt > int.MaxValue ? int.MaxValue : (int)bigInt;
            }
            else
            {
                blockCount = Convert.ToInt32(blockCountParam);
            }
            return true;
        }

        private static async Task<BigInteger> ResolveNewestBlockAsync(string newestBlockTag, RpcContext context)
        {
            if (newestBlockTag == BlockParameter.BlockParameterType.latest.ToString() || newestBlockTag == BlockParameter.BlockParameterType.pending.ToString())
            {
                return await context.Node.GetBlockNumberAsync();
            }
            if (newestBlockTag == BlockParameter.BlockParameterType.earliest.ToString())
            {
                return 0;
            }
            return newestBlockTag.HexToBigInteger(false);
        }

        private async Task<Model.BlockHeader> CollectRowsAsync(
            RpcContext context,
            List<HexBigInteger> baseFeePerGas,
            List<decimal> gasUsedRatio,
            List<HexBigInteger> baseFeePerBlobGas,
            List<decimal> blobGasUsedRatio,
            List<HexBigInteger[]> reward,
            List<double> rewardPercentiles,
            BigInteger oldestBlock,
            BigInteger newestBlock)
        {
            Model.BlockHeader newestHeader = null;

            for (var i = oldestBlock; i <= newestBlock; i++)
            {
                try
                {
                    var block = await context.Node.GetBlockByNumberAsync(i);
                    if (block != null)
                    {
                        if (i == newestBlock) newestHeader = block;

                        var blockBaseFee = block.BaseFee ?? context.Node.Config.BaseFee;
                        baseFeePerGas.Add(new HexBigInteger(blockBaseFee));
                        var ratio = block.GasLimit > 0 ? (decimal)block.GasUsed / (decimal)block.GasLimit : 0m;
                        gasUsedRatio.Add(ratio);
                        baseFeePerBlobGas.Add(BlobBaseFeeForBlock(context, block));
                        blobGasUsedRatio.Add(BlobGasUsedRatioForBlock(context, block));

                        if (reward != null && rewardPercentiles != null)
                        {
                            var blockHash = await context.Node.GetBlockHashByNumberAsync(i);
                            var blockRewards = await CalculateRewardPercentilesAsync(
                                context, blockHash, blockBaseFee, rewardPercentiles);
                            reward.Add(blockRewards);
                        }
                    }
                    else
                    {
                        AddDefaultBlockEntry(context, baseFeePerGas, gasUsedRatio, baseFeePerBlobGas, blobGasUsedRatio, reward, rewardPercentiles);
                    }
                }
                catch
                {
                    AddDefaultBlockEntry(context, baseFeePerGas, gasUsedRatio, baseFeePerBlobGas, blobGasUsedRatio, reward, rewardPercentiles);
                }
            }

            return newestHeader;
        }

        private static void AddDefaultBlockEntry(
            RpcContext context,
            List<HexBigInteger> baseFeePerGas,
            List<decimal> gasUsedRatio,
            List<HexBigInteger> baseFeePerBlobGas,
            List<decimal> blobGasUsedRatio,
            List<HexBigInteger[]> reward,
            List<double> rewardPercentiles)
        {
            baseFeePerGas.Add(new HexBigInteger(context.Node.Config.BaseFee));
            gasUsedRatio.Add(0m);
            baseFeePerBlobGas.Add(new HexBigInteger(0));
            blobGasUsedRatio.Add(0m);
            if (reward != null && rewardPercentiles != null)
            {
                reward.Add(rewardPercentiles.Select(_ => new HexBigInteger(0)).ToArray());
            }
        }

        private static HexBigInteger BlobBaseFeeForBlock(RpcContext context, Model.BlockHeader block)
        {
            if (block?.ExcessBlobGas == null) return new HexBigInteger(0);

            var hardforkConfig = context.Node.Config.GetHardforkConfigAt(
                (long)block.BlockNumber, (ulong)block.Timestamp);
            var blobRule = hardforkConfig.IntrinsicGasRules.Blob;
            var excess = (EvmUInt256)(ulong)block.ExcessBlobGas.Value;
            var fee = blobRule != null
                ? blobRule.CalculateBlobBaseFee(excess)
                : Model.BlobGasCalculator.CalculateBlobBaseFee(excess);
            return new HexBigInteger((BigInteger)fee);
        }

        private static decimal BlobGasUsedRatioForBlock(RpcContext context, Model.BlockHeader block)
        {
            if (block?.BlobGasUsed == null) return 0m;

            var maxBlobs = context.Node.Config.GetHardforkConfigAt(
                (long)block.BlockNumber, (ulong)block.Timestamp).MaxBlobsPerBlock;
            if (maxBlobs <= 0) return 0m;

            return (decimal)block.BlobGasUsed.Value / ((decimal)maxBlobs * Model.BlobGasCalculator.GAS_PER_BLOB);
        }

        private static BigInteger ProjectedNextBaseFee(RpcContext context, Model.BlockHeader newest)
        {
            if (newest == null || context.Node.Config.FeeFreeBaseFee) return context.Node.Config.BaseFee;
            return (BigInteger)Model.BaseFeeCalculator.CalculateExpectedBaseFeePerGas(
                newest.BaseFee, newest.GasLimit, newest.GasUsed);
        }

        private static HexBigInteger ProjectedNextBlobBaseFee(RpcContext context, Model.BlockHeader newest)
        {
            if (newest?.ExcessBlobGas == null) return new HexBigInteger(0);

            var hardforkConfig = context.Node.Config.GetHardforkConfigAt(
                (long)newest.BlockNumber, (ulong)newest.Timestamp);
            var blobRule = hardforkConfig.IntrinsicGasRules.Blob;
            if (blobRule == null) return new HexBigInteger(0);

            var excess = (EvmUInt256)BlockExecutor.ExpectedExcessBlobGas(newest, hardforkConfig);
            var fee = blobRule.CalculateBlobBaseFee(excess);
            return new HexBigInteger((BigInteger)fee);
        }

        private async Task<HexBigInteger[]> CalculateRewardPercentilesAsync(
            RpcContext context,
            byte[] blockHash,
            BigInteger baseFee,
            List<double> percentiles)
        {
            var transactions = await context.Node.Transactions.GetByBlockHashAsync(blockHash);
            if (transactions == null || transactions.Count == 0)
            {
                return percentiles.Select(_ => new HexBigInteger(0)).ToArray();
            }

            var priorityFees = new List<BigInteger>();
            foreach (var tx in transactions)
            {
                var priorityFee = CalculateEffectivePriorityFee(tx, baseFee);
                priorityFees.Add(priorityFee);
            }

            priorityFees.Sort();

            var results = new HexBigInteger[percentiles.Count];
            for (int p = 0; p < percentiles.Count; p++)
            {
                var index = (int)Math.Floor(percentiles[p] / 100.0 * (priorityFees.Count - 1));
                index = Math.Max(0, Math.Min(index, priorityFees.Count - 1));
                results[p] = new HexBigInteger(priorityFees[index]);
            }

            return results;
        }

        private EvmUInt256 CalculateEffectivePriorityFee(ISignedTransaction tx, EvmUInt256 baseFee)
        {
            if (tx is Transaction1559 tx1559)
            {
                var maxPriorityFee = tx1559.MaxPriorityFeePerGas ?? EvmUInt256.Zero;
                var maxFee = tx1559.MaxFeePerGas ?? EvmUInt256.Zero;
                var diff = maxFee - baseFee;
                return maxPriorityFee < diff ? maxPriorityFee : diff;
            }
            if (tx is Transaction2930 tx2930)
            {
                var gasPrice = tx2930.GasPrice ?? EvmUInt256.Zero;
                return gasPrice > baseFee ? gasPrice - baseFee : EvmUInt256.Zero;
            }
            if (tx is LegacyTransaction legacyTx)
            {
                var gasPrice = legacyTx.GasPrice.ToEvmUInt256FromRLPDecoded();
                return gasPrice > baseFee ? gasPrice - baseFee : EvmUInt256.Zero;
            }
            if (tx is LegacyTransactionChainId legacyChainIdTx)
            {
                var gasPrice = legacyChainIdTx.GasPrice.ToEvmUInt256FromRLPDecoded();
                return gasPrice > baseFee ? gasPrice - baseFee : EvmUInt256.Zero;
            }
            return EvmUInt256.Zero;
        }
    }
}
