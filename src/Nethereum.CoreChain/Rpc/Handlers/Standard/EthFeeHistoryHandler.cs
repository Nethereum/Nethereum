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
using Nethereum.RLP;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthFeeHistoryHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_feeHistory.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                return await HandleCoreAsync(request, context);
            }
            catch
            {
                return BuildDefaultResponse(request, context.Node.Config.BaseFee);
            }
        }

        private RpcResponseMessage BuildDefaultResponse(RpcRequestMessage request, BigInteger baseFee)
        {
            var baseFeeHex = new HexBigInteger(baseFee);
            var result = new FeeHistoryResult
            {
                OldestBlock = new HexBigInteger(0),
                BaseFeePerGas = new[] { baseFeeHex, baseFeeHex },
                GasUsedRatio = new[] { 0m },
                Reward = new[] { new[] { new HexBigInteger(0) } }
            };
            return Success(request.Id, result);
        }

        private async Task<RpcResponseMessage> HandleCoreAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockCountParam = GetParam<object>(request, 0);
            var newestBlockTag = GetParam<string>(request, 1);
            var rewardPercentiles = GetOptionalParam<List<double>>(request, 2, null);

            int blockCount;
            if (blockCountParam is string hexStr)
            {
                blockCount = (int)hexStr.HexToBigInteger(false);
            }
            else if (blockCountParam is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    blockCount = (int)jsonElement.GetString().HexToBigInteger(false);
                }
                else if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    blockCount = jsonElement.GetInt32();
                }
                else
                {
                    return BuildDefaultResponse(request, context.Node.Config.BaseFee);
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

            blockCount = Math.Min(blockCount, 1024);

            BigInteger newestBlock;
            if (newestBlockTag == BlockParameter.BlockParameterType.latest.ToString() || newestBlockTag == BlockParameter.BlockParameterType.pending.ToString())
            {
                newestBlock = await context.Node.GetBlockNumberAsync();
            }
            else if (newestBlockTag == BlockParameter.BlockParameterType.earliest.ToString())
            {
                newestBlock = 0;
            }
            else
            {
                newestBlock = newestBlockTag.HexToBigInteger(false);
            }

            if (newestBlock < 0) newestBlock = 0;

            var baseFeePerGas = new List<HexBigInteger>();
            var gasUsedRatio = new List<decimal>();
            var reward = rewardPercentiles != null ? new List<HexBigInteger[]>() : null;

            var oldestBlock = BigInteger.Max(0, newestBlock - blockCount + 1);

            for (var i = oldestBlock; i <= newestBlock; i++)
            {
                try
                {
                    var block = await context.Node.GetBlockByNumberAsync(i);
                    if (block != null)
                    {
                        var blockBaseFee = block.BaseFee ?? context.Node.Config.BaseFee;
                        baseFeePerGas.Add(new HexBigInteger(blockBaseFee));
                        var ratio = block.GasLimit > 0 ? (decimal)block.GasUsed / (decimal)block.GasLimit : 0m;
                        gasUsedRatio.Add(ratio);

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
                        AddDefaultBlockEntry(context, baseFeePerGas, gasUsedRatio, reward, rewardPercentiles);
                    }
                }
                catch
                {
                    AddDefaultBlockEntry(context, baseFeePerGas, gasUsedRatio, reward, rewardPercentiles);
                }
            }

            if (baseFeePerGas.Count == 0)
            {
                baseFeePerGas.Add(new HexBigInteger(context.Node.Config.BaseFee));
                gasUsedRatio.Add(0m);
                if (reward != null && rewardPercentiles != null)
                {
                    reward.Add(rewardPercentiles.Select(_ => new HexBigInteger(0)).ToArray());
                }
            }

            baseFeePerGas.Add(new HexBigInteger(context.Node.Config.BaseFee));

            var result = new FeeHistoryResult
            {
                OldestBlock = new HexBigInteger(oldestBlock),
                BaseFeePerGas = baseFeePerGas.ToArray(),
                GasUsedRatio = gasUsedRatio.ToArray(),
                Reward = reward?.ToArray()
            };

            return Success(request.Id, result);
        }

        private static void AddDefaultBlockEntry(
            RpcContext context,
            List<HexBigInteger> baseFeePerGas,
            List<decimal> gasUsedRatio,
            List<HexBigInteger[]> reward,
            List<double> rewardPercentiles)
        {
            baseFeePerGas.Add(new HexBigInteger(context.Node.Config.BaseFee));
            gasUsedRatio.Add(0m);
            if (reward != null && rewardPercentiles != null)
            {
                reward.Add(rewardPercentiles.Select(_ => new HexBigInteger(0)).ToArray());
            }
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

        private BigInteger CalculateEffectivePriorityFee(ISignedTransaction tx, BigInteger baseFee)
        {
            if (tx is Transaction1559 tx1559)
            {
                var maxPriorityFee = tx1559.MaxPriorityFeePerGas ?? BigInteger.Zero;
                var maxFee = tx1559.MaxFeePerGas ?? BigInteger.Zero;
                return BigInteger.Min(maxPriorityFee, maxFee - baseFee);
            }
            if (tx is Transaction2930 tx2930)
            {
                var gasPrice = tx2930.GasPrice ?? BigInteger.Zero;
                return gasPrice > baseFee ? gasPrice - baseFee : BigInteger.Zero;
            }
            if (tx is LegacyTransaction legacyTx)
            {
                var gasPrice = legacyTx.GasPrice.ToBigIntegerFromRLPDecoded();
                return gasPrice > baseFee ? gasPrice - baseFee : BigInteger.Zero;
            }
            if (tx is LegacyTransactionChainId legacyChainIdTx)
            {
                var gasPrice = legacyChainIdTx.GasPrice.ToBigIntegerFromRLPDecoded();
                return gasPrice > baseFee ? gasPrice - baseFee : BigInteger.Zero;
            }
            return BigInteger.Zero;
        }
    }
}
