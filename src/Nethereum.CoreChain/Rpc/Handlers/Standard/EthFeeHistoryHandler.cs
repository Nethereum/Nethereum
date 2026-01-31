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
                    throw RpcException.InvalidParams("Invalid block count parameter");
                }
            }
            else if (blockCountParam is HexBigInteger hexBigInt)
            {
                blockCount = (int)hexBigInt.Value;
            }
            else if (blockCountParam is BigInteger bigInt)
            {
                blockCount = (int)bigInt;
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

            var baseFeePerGas = new List<string>();
            var gasUsedRatio = new List<double>();
            var reward = rewardPercentiles != null ? new List<List<string>>() : null;

            var oldestBlock = BigInteger.Max(0, newestBlock - blockCount + 1);

            for (var i = oldestBlock; i <= newestBlock; i++)
            {
                var block = await context.Node.GetBlockByNumberAsync(i);
                if (block != null)
                {
                    var blockBaseFee = block.BaseFee ?? context.Node.Config.BaseFee;
                    baseFeePerGas.Add(new HexBigInteger(blockBaseFee).HexValue);
                    var ratio = block.GasLimit > 0 ? (double)block.GasUsed / (double)block.GasLimit : 0;
                    gasUsedRatio.Add(ratio);

                    if (reward != null && rewardPercentiles != null)
                    {
                        var blockHash = await context.Node.GetBlockHashByNumberAsync(i);
                        var blockRewards = await CalculateRewardPercentilesAsync(
                            context, blockHash, block.BaseFee ?? context.Node.Config.BaseFee, rewardPercentiles);
                        reward.Add(blockRewards);
                    }
                }
                else
                {
                    baseFeePerGas.Add(new HexBigInteger(context.Node.Config.BaseFee).HexValue);
                    gasUsedRatio.Add(0);
                    if (reward != null && rewardPercentiles != null)
                    {
                        var blockRewards = new List<string>();
                        foreach (var percentile in rewardPercentiles)
                        {
                            blockRewards.Add(new HexBigInteger(0).HexValue);
                        }
                        reward.Add(blockRewards);
                    }
                }
            }

            baseFeePerGas.Add(new HexBigInteger(context.Node.Config.BaseFee).HexValue);

            var result = new Dictionary<string, object>
            {
                ["oldestBlock"] = new HexBigInteger(oldestBlock).HexValue,
                ["baseFeePerGas"] = baseFeePerGas,
                ["gasUsedRatio"] = gasUsedRatio
            };

            if (reward != null)
            {
                result["reward"] = reward;
            }

            return Success(request.Id, result);
        }

        private async Task<List<string>> CalculateRewardPercentilesAsync(
            RpcContext context,
            byte[] blockHash,
            BigInteger baseFee,
            List<double> percentiles)
        {
            var transactions = await context.Node.Transactions.GetByBlockHashAsync(blockHash);
            if (transactions == null || transactions.Count == 0)
            {
                return percentiles.Select(_ => new HexBigInteger(0).HexValue).ToList();
            }

            var priorityFees = new List<BigInteger>();
            foreach (var tx in transactions)
            {
                var priorityFee = CalculateEffectivePriorityFee(tx, baseFee);
                priorityFees.Add(priorityFee);
            }

            priorityFees.Sort();

            var results = new List<string>();
            foreach (var percentile in percentiles)
            {
                var index = (int)Math.Floor(percentile / 100.0 * (priorityFees.Count - 1));
                index = Math.Max(0, Math.Min(index, priorityFees.Count - 1));
                results.Add(new HexBigInteger(priorityFees[index]).HexValue);
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
