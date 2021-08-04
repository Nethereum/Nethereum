using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Util;

namespace Nethereum.RPC.Fee1559Suggestions
{
    /// <summary>
    /// Suggest a priority fee based on the Fee history of previous blocks and the median of all its values
    /// Base fee is suggested based on the latest block number price and depending on its value increase it by a percentage.
    /// This is based on MyCrypto example here https://github.com/MyCryptoHQ/MyCrypto/blob/master/src/services/ApiService/Gas/eip1559.ts
    /// </summary>
    public class MedianPriorityFeeHistorySuggestionStrategy:IFee1559SuggestionStrategy
    {
        public IClient Client { get; set; }
        private IEthGetBlockWithTransactionsHashesByNumber _ethGetBlockWithTransactionsHashes;
        private IEthFeeHistory _ethFeeHistory;


        ///<summary>
        ///How many blocks to consider for priority fee estimation
        /// </summary>
        public static int FeeHistoryNumberOfBlocks { get; set; } = 10;
        ///<summary>
        // Which percentile of effective priority fees to include
        /// </summary>
        public const double FEE_HISTORY_PERCENTILE = 5;
        // Which base fee to trigger priority fee estimation at
        public const int PRIORITY_FEE_ESTIMATION_TRIGGER = 100; // GWEI
                                                         // Returned if above trigger is not met
        public static long DefaultPriorityFee { get; set; } = 3_000_000_000;


        public static Fee1559 FallbackFeeSuggestion { get; set; } = new Fee1559()
        {
            MaxFeePerGas = 20_000_000_000,
            MaxPriorityFeePerGas = DefaultPriorityFee
        };

        public const int PRIORITY_FEE_INCREASE_BOUNDARY = 200;

        public virtual BigDecimal GetBaseFeeMultiplier(BigDecimal baseFeeGwei)
        {
            if (baseFeeGwei < 40)
            {
                return 2.0;
            }
            else if (baseFeeGwei < 100)
            {
                return 1.6;
            }
            else if (baseFeeGwei < 200)
            {
                return 1.4;
            }
            else
            {
                return 1.2;
            }
        }


        public MedianPriorityFeeHistorySuggestionStrategy(IClient client)
        {
            Client = client;
            _ethGetBlockWithTransactionsHashes = new EthGetBlockWithTransactionsHashesByNumber(client);
            _ethFeeHistory = new EthFeeHistory(client);
        }

        public async Task<BigInteger?> EstimatePriorityFee(decimal baseFeeGwei, HexBigInteger blockNumber)
        {
            if (baseFeeGwei < PRIORITY_FEE_ESTIMATION_TRIGGER)
            {
                return DefaultPriorityFee;
            }

            var feeHistory = await _ethFeeHistory.SendRequestAsync(new HexBigInteger(FeeHistoryNumberOfBlocks), new BlockParameter(blockNumber), new double[] {
              FEE_HISTORY_PERCENTILE }
            ).ConfigureAwait(false);

            var rewards = feeHistory.Reward
              ?.Select((r) => r[0].Value)
              .Where((r) => r != 0)
              .ToList();
            
            rewards.Sort();


            if (rewards == null || rewards.Count == 0)
            {
                return null;
            }

            var percentageIncreases = new List<BigInteger>();

            for(var i=0; i < rewards.Count - 1; i++)
            {
                var next = rewards[i + 1];
                var p = ((next - rewards[i]) / rewards[i]) * 100;
                percentageIncreases.Add(p);
            }
            
           
            var highestIncrease = percentageIncreases.Max();
            var highestIncreaseIndex = percentageIncreases.IndexOf(highestIncrease);

            //// If we have big increase in value, we could be considering "outliers" in our estimate
            //// Skip the low elements and take a new median
            var values =
              (highestIncrease > PRIORITY_FEE_INCREASE_BOUNDARY &&
              highestIncreaseIndex >= Math.Floor((double)(rewards.Count / 2))
                ? rewards.Skip(highestIncreaseIndex)
                : rewards).ToArray();

            var valuesIndex = (int)Math.Floor((double)(values.Length / 2));
            return values[valuesIndex];
        }

        public async Task<Fee1559> SuggestFeeAsync(BigInteger? maxPriorityFeePerGas = null)
        {
            
            var lastBlock = await _ethGetBlockWithTransactionsHashes.SendRequestAsync(BlockParameter.CreateLatest()).ConfigureAwait(false);

            if (lastBlock.BaseFeePerGas == null)
            {
                return FallbackFeeSuggestion;
            }

            var baseFee = lastBlock.BaseFeePerGas;

            var baseFeeGwei = UnitConversion.Convert.FromWei(baseFee, UnitConversion.EthUnit.Gwei);

            if (maxPriorityFeePerGas == null)
            { 
                var estimatedPriorityFee = await EstimatePriorityFee(
                    baseFeeGwei,
                    lastBlock.Number
                );

                if (estimatedPriorityFee == null)
                {
                    return FallbackFeeSuggestion;
                }

                maxPriorityFeePerGas = BigInteger.Max(estimatedPriorityFee.Value, DefaultPriorityFee);
            }

            var multiplier = GetBaseFeeMultiplier(baseFeeGwei);

            var potentialMaxFee = (BigDecimal)baseFee.Value * multiplier;
            var maxFeePerGas = maxPriorityFeePerGas > potentialMaxFee
                ? potentialMaxFee + maxPriorityFeePerGas
                : potentialMaxFee;

            return new Fee1559()
            {
                MaxFeePerGas = maxFeePerGas.Value.Floor().Mantissa,
                MaxPriorityFeePerGas = maxPriorityFeePerGas,
                BaseFee = baseFee
            };
            
        }
    }

}
