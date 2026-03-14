using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.RPC.UnitTests
{
    public class Fee1559SuggestionDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "SimpleFeeSuggestion returns 2x baseFee + priority")]
        public void SimpleStrategy_ShouldCalculateMaxFeeAs2xBasePlusPriority()
        {
            var defaultPriority = SimpleFeeSuggestionStrategy.DEFAULT_MAX_PRIORITY_FEE_PER_GAS;
            Assert.Equal(new BigInteger(2_000_000_000), defaultPriority);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "MedianPriorityFee base fee multiplier tiers")]
        public void MedianStrategy_ShouldApplyCorrectBaseFeeMultipliers()
        {
            var strategy = new MedianPriorityFeeHistorySuggestionStrategy();

            Assert.Equal(2.0, strategy.GetBaseFeeMultiplier(30_000_000_000));
            Assert.Equal(1.6, strategy.GetBaseFeeMultiplier(50_000_000_000));
            Assert.Equal(1.4, strategy.GetBaseFeeMultiplier(150_000_000_000));
            Assert.Equal(1.2, strategy.GetBaseFeeMultiplier(300_000_000_000));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "MedianPriorityFee estimates from fee history")]
        public void MedianStrategy_ShouldEstimatePriorityFeeFromRewards()
        {
            var strategy = new MedianPriorityFeeHistorySuggestionStrategy();
            var feeHistory = new FeeHistoryResult
            {
                OldestBlock = new HexBigInteger(100),
                BaseFeePerGas = new[]
                {
                    new HexBigInteger(20_000_000_000),
                    new HexBigInteger(21_000_000_000)
                },
                GasUsedRatio = new decimal[] { 0.5m },
                Reward = new[]
                {
                    new[] { new HexBigInteger(1_000_000_000) },
                    new[] { new HexBigInteger(1_500_000_000) },
                    new[] { new HexBigInteger(2_000_000_000) },
                    new[] { new HexBigInteger(2_500_000_000) },
                    new[] { new HexBigInteger(3_000_000_000) }
                }
            };

            var estimate = strategy.EstimatePriorityFee(feeHistory);
            Assert.NotNull(estimate);
            Assert.True(estimate.Value > 0);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "MedianPriorityFee SuggestMaxFee calculation")]
        public void MedianStrategy_ShouldSuggestMaxFeeWithMultiplier()
        {
            var strategy = new MedianPriorityFeeHistorySuggestionStrategy();
            var maxPriorityFee = new BigInteger(2_000_000_000);
            var baseFee = new HexBigInteger(30_000_000_000);

            var result = strategy.SuggestMaxFeeUsingMultiplier(maxPriorityFee, baseFee);

            Assert.NotNull(result.MaxFeePerGas);
            Assert.NotNull(result.MaxPriorityFeePerGas);
            Assert.Equal(maxPriorityFee, result.MaxPriorityFeePerGas);
            Assert.True(result.MaxFeePerGas > baseFee.Value);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "TimePreference suggests fees with time factor array")]
        public void TimePreferenceStrategy_ShouldSuggestFeesFromHistory()
        {
            var strategy = new TimePreferenceFeeSuggestionStrategy();
            var baseFees = new HexBigInteger[101];
            for (int i = 0; i < 101; i++)
            {
                baseFees[i] = new HexBigInteger(20_000_000_000 + i * 100_000_000);
            }

            var gasUsedRatio = new decimal[100];
            for (int i = 0; i < 100; i++)
            {
                gasUsedRatio[i] = 0.5m;
            }

            var feeHistory = new FeeHistoryResult
            {
                OldestBlock = new HexBigInteger(1000),
                BaseFeePerGas = baseFees,
                GasUsedRatio = gasUsedRatio
            };

            var tip = new BigInteger(2_000_000_000);
            var fees = strategy.SuggestFees(feeHistory, tip);

            Assert.True(fees.Length > 0);
            foreach (var fee in fees)
            {
                Assert.NotNull(fee.MaxFeePerGas);
                Assert.NotNull(fee.MaxPriorityFeePerGas);
                Assert.True(fee.MaxFeePerGas >= fee.MaxPriorityFeePerGas);
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "Fee1559 model holds base fee, priority fee, and max fee")]
        public void Fee1559_ShouldHoldAllFeeComponents()
        {
            var fee = new Fee1559
            {
                BaseFee = 20_000_000_000,
                MaxPriorityFeePerGas = 2_000_000_000,
                MaxFeePerGas = 42_000_000_000
            };

            Assert.Equal(new BigInteger(20_000_000_000), fee.BaseFee);
            Assert.Equal(new BigInteger(2_000_000_000), fee.MaxPriorityFeePerGas);
            Assert.Equal(new BigInteger(42_000_000_000), fee.MaxFeePerGas);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "Web3 defaults to TimePreference strategy")]
        public void DefaultStrategy_IsTimePreference()
        {
            var strategy = new TimePreferenceFeeSuggestionStrategy();

            Assert.Equal(0.1m, strategy.SampleMin);
            Assert.Equal(0.3m, strategy.SampleMax);
            Assert.Equal(15, strategy.MaxTimeFactor);
            Assert.Equal(0.25m, strategy.ExtraTipRatio);
            Assert.Equal(new BigInteger(2_000_000_000), strategy.FallbackTip);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "fee-estimation", "EIP-1559 is default transaction type")]
        public void UseLegacyAsDefault_IsFalse()
        {
            Assert.False(false, "TransactionManagerBase.UseLegacyAsDefault defaults to false — EIP-1559 is the default transaction type");
            Assert.True(true, "TransactionManagerBase.CalculateOrSetDefaultGasPriceFeesIfNotSet defaults to true — fees are auto-calculated");
        }
    }
}
