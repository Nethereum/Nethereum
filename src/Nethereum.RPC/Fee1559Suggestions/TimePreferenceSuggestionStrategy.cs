using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Util;

namespace Nethereum.RPC.Fee1559Suggestions
{
#if !DOTNET35

    /// <summary>
    /// SuggestFees returns a series of maxFeePerGas / maxPriorityFeePerGas values suggested for different time preferences.The first element corresponds to the highest time preference(most urgent transaction).
    /// The basic idea behind the algorithm is similar to the old "gas price oracle" used in Geth; it takes the prices of recent blocks and makes a suggestion based on a low percentile of those prices.With EIP-1559 though the base fee of each block provides a less noisy and more reliable price signal.
    /// This allows for more sophisticated suggestions with a variable width(exponentially weighted) base fee time window.
    /// The window width corresponds to the time preference of the user.
    /// The underlying assumption is that price fluctuations over a given past time period indicate the probability of similar price levels being re-tested by the market over a similar length future time period.
    /// This is a port of Felfodi Zsolt example https://github.com/zsfelfoldi/ethereum-docs/blob/master/eip1559/feeHistory_example.js
    /// </summary>
    public class TimePreferenceSuggestionStrategy:IFee1559SugesstionStrategy
    {
        public double SampleMin { get; set; } = 0.1;
        public double SampleMax { get; set; }  = 0.3;
        public int MaxTimeFactor { get; set; } = 15;
        public double ExtraTipRatio { get; set; } = 0.25;
        public BigInteger FallbackTip { get; set; } = 5000000000;

        public IClient Client { get; set; }
        private EthFeeHistory ethFeeHistory;

        public TimePreferenceSuggestionStrategy(IClient client)
        {
            ethFeeHistory = new EthFeeHistory(client);
        }

        /// <summary>
        /// Suggest fee returns the first element of the time preferences, this is highest time preference(most urgent transaction).
        /// The maxPriorityFeePerGas if supplied will override the calculated value, and if is bigger than the MaxFeePerGas calculated, it will be also overriden with the same value
        /// </summary>
        public async Task<Fee1559> SuggestFeeAsync(BigInteger? maxPriorityFeePerGas = null)
        {
            var fees = await SuggestFeesAsync().ConfigureAwait(false);
            var returnFee = fees.First(); // using the first fee as it is the fastest
            if (maxPriorityFeePerGas != null)
            {
                returnFee.MaxPriorityFeePerGas = maxPriorityFeePerGas;
                if (returnFee.MaxFeePerGas < maxPriorityFeePerGas)
                {
                    returnFee.MaxFeePerGas = maxPriorityFeePerGas;
                }
            }
            return returnFee;
        }


        /// <summary>
        ///SuggestFees returns a series of maxFeePerGas / maxPriorityFeePerGas values suggested for different time preferences.The first element corresponds to the highest time preference(most urgent transaction).
        /// The basic idea behind the algorithm is similar to the old "gas price oracle" used in Geth; it takes the prices of recent blocks and makes a suggestion based on a low percentile of those prices.With EIP-1559 though the base fee of each block provides a less noisy and more reliable price signal.
        /// This allows for more sophisticated suggestions with a variable width(exponentially weighted) base fee time window.
        /// The window width corresponds to the time preference of the user.
        /// The underlying assumption is that price fluctuations over a given past time period indicate the probability of similar price levels being re-tested by the market over a similar length future time period.
        /// </summary>
        public async Task<Fee1559[]> SuggestFeesAsync()
        {
            // feeHistory API call without a reward percentile specified is cheap even with a light client backend because it only needs block headers.
            // Therefore we can afford to fetch a hundred blocks of base fee history in order to make meaningful estimates on variable time scales.
            var feeHistory = await ethFeeHistory.SendRequestAsync(100, BlockParameter.CreateLatest()).ConfigureAwait(false);
            var baseFee = feeHistory.BaseFeePerGas.Select( x => x.Value).ToArray();
            var gasUsedRatio = feeHistory.GasUsedRatio;


            // If a block is full then the baseFee of the next block is copied. The reason is that in full blocks the minimal tip might not be enough to get included.
            // The last (pending) block is also assumed to end up being full in order to give some upwards bias for urgent suggestions.
            baseFee[baseFee.Length - 1] *= 9 / 8;
            for (var i = gasUsedRatio.Length - 1; i >= 0; i--)
            {
                if (gasUsedRatio[i] > (decimal) 0.9)
                {
                    baseFee[i] = baseFee[i + 1];
                }
            }

            var order = new int[baseFee.Length];
            for (var i = 0; i < baseFee.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, Comparer<int>.Create((int x, int y) =>
            {
                var aa = baseFee[x];
                var bb = baseFee[y];
                if (aa < bb)
                {
                    return -1;
                }
                if (aa > bb)
                {
                    return 1;
                }
                return 0;

            }));


            var tip = await SuggestTip(feeHistory.OldestBlock, gasUsedRatio).ConfigureAwait(false);
         
            var result = new List<Fee1559>();
            BigDecimal maxBaseFee = 0;
            for (var timeFactor = MaxTimeFactor; timeFactor >= 0; timeFactor--)
            {
                var bf = SuggestBaseFee(baseFee, order, timeFactor);
                BigDecimal t = new BigDecimal(tip, 0);
                if (bf > maxBaseFee)
                {
                    maxBaseFee = bf;
                }
                else
                {
                    // If a narrower time window yields a lower base fee suggestion than a wider window then we are probably in a price dip.
                    // In this case getting included with a low tip is not guaranteed; instead we use the higher base fee suggestion
                    // and also offer extra tip to increase the chance of getting included in the base fee dip.
                    t += (maxBaseFee - bf) * ExtraTipRatio;
                    bf = maxBaseFee;
                }
                result.Add(new Fee1559() {
                    BaseFee =  bf.Floor().Mantissa,
                    MaxFeePerGas = (bf + t).Floor().Mantissa,
                    MaxPriorityFeePerGas = t.Floor().Mantissa
                });
            }

            result.Reverse();
            return result.ToArray();
        }

        /// <summary>
        /// // suggestBaseFee calculates an average of base fees in the sampleMin to sampleMax percentile range of recent base fee history, each block weighted with an exponential time function based on timeFactor.
        /// </summary>
        private BigDecimal SuggestBaseFee(BigInteger[] baseFee, int[] order, int timeFactor)
        { 
            if (timeFactor < 1e-6)
            {
                return new BigDecimal(baseFee[baseFee.Length - 1], 0);
            }
            var pendingWeight = (1 - Math.Exp(-1 / timeFactor)) / (1 - Math.Exp(-baseFee.Length / timeFactor));
            double sumWeight = 0;
            BigDecimal result = 0;
            double samplingCurveLast = 0;
            for (var i = 0; i < order.Length; i++)
            {
                sumWeight += pendingWeight * Math.Exp((order[i] - baseFee.Length + 1) / timeFactor);
                var samplingCurveValue = SamplingCurve(sumWeight);
                result += (samplingCurveValue - samplingCurveLast) * new BigDecimal(baseFee[order[i]], 0);
                if (samplingCurveValue >= 1)
                {
                    return result;
                }
                samplingCurveLast = samplingCurveValue;
            }
            return result;
            
        }

        // samplingCurve is a helper function for the base fee percentile range calculation.
        private double SamplingCurve(double sumWeight)
        {
            if (sumWeight <= SampleMin)
            {
                return 0;
            }
            if (sumWeight >= SampleMax)
            {
                return 1;
            }
            return (1 - Math.Cos((sumWeight - SampleMin) * 2 * Math.PI / (SampleMax - SampleMin))) / 2;
        }

        private async Task<BigInteger> SuggestTip(BigInteger firstBlock, decimal[] gasUsedRatio)
        {
            var ptr = gasUsedRatio.Length - 1;
            var needBlocks = 5;
            var rewards = new List<BigInteger>();
            while (needBlocks > 0 && ptr >= 0)
            {
                var blockCount = MaxBlockCount(gasUsedRatio, ptr, needBlocks);
                if (blockCount > 0)
                {
                    // feeHistory API call with reward percentile specified is expensive and therefore is only requested for a few non-full recent blocks.
                    var feeHistory = await ethFeeHistory.SendRequestAsync((uint)blockCount, new BlockParameter(new HexBigInteger(firstBlock + ptr)), new int[]{10}).ConfigureAwait(false);
                    for (var i = 0; i < feeHistory.Reward.Length; i++)
                    {
                        rewards.Add(feeHistory.Reward[i][0]);
                    }
                    if (feeHistory.Reward.Length < blockCount)
                    {
                        break;
                    }
                    needBlocks -= blockCount;
                }
                ptr -= blockCount + 1;
            }

            if (rewards.Count == 0)
            {
                return FallbackTip;
            }
            rewards.Sort();
            return rewards[(int) Math.Truncate((double) (rewards.Count / 2))];
        }


        // maxBlockCount returns the number of consecutive blocks suitable for tip suggestion (gasUsedRatio between 0.1 and 0.9).
        public int MaxBlockCount(decimal[] gasUsedRatio, int ptr, int needBlocks)
        {
            int blockCount = 0;
            while (needBlocks > 0 && ptr >= 0)
            {
                if (gasUsedRatio[ptr] < (decimal) 0.1 || gasUsedRatio[ptr] > (decimal) 0.9)
                {
                    break;
                }
                ptr--;
                needBlocks--;
                blockCount++;
            }
            return blockCount;
        }
    }

#endif
}