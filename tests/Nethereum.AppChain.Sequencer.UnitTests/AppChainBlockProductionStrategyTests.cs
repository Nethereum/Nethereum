using System;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class AppChainBlockProductionStrategyTests
    {
        [Theory]
        [InlineData(30_000_000L, 60_000_000L)]
        [InlineData(60_000_000L, 30_000_000L)]
        public void Given_AConfiguredGasLimitFarFromTheParents_When_TheNextBlockIsPrepared_Then_ItStaysInsideTheParentBound(
            long parentGasLimit, long configuredGasLimit)
        {
            var config = AppChainConfig.CreateWithName("GasLimitStep", 420420);
            config.BlockGasLimit = configuredGasLimit;
            var strategy = new AppChainBlockProductionStrategy(config);

            var options = strategy.PrepareBlockOptions(2, new BlockHeader { BlockNumber = 1, GasLimit = parentGasLimit });

            var difference = BigInteger.Abs(options.BlockGasLimit - parentGasLimit);
            Assert.True(difference < parentGasLimit / GasLimitBound.BoundDivisor);
            Assert.Equal(GasLimitBound.StepToward(parentGasLimit, configuredGasLimit), options.BlockGasLimit);
        }

        [Fact]
        public void Given_NoParent_When_TheFirstBlockIsPrepared_Then_ItUsesTheConfiguredGasLimit()
        {
            var config = AppChainConfig.CreateWithName("GasLimitStep", 420420);
            config.BlockGasLimit = 45_000_000;

            var options = new AppChainBlockProductionStrategy(config).PrepareBlockOptions(1, null);

            Assert.Equal(new BigInteger(45_000_000), options.BlockGasLimit);
        }

        [Fact]
        public void Given_AParentSealedInTheCurrentSecond_When_TheNextBlockIsPrepared_Then_ItsTimestampIsTheParentsPlusOne()
        {
            var config = AppChainConfig.CreateWithName("Timestamps", 420420);
            var parentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1_000;

            var options = new AppChainBlockProductionStrategy(config).PrepareBlockOptions(
                2, new BlockHeader { BlockNumber = 1, GasLimit = 30_000_000, Timestamp = parentTimestamp });

            Assert.Equal(parentTimestamp + 1, options.Timestamp);
        }

        [Fact]
        public void Given_AChainNameLongerThanTheExtraDataLimit_When_ABlockIsPrepared_Then_TheExtraDataFitsIn32Bytes()
        {
            var config = AppChainConfig.CreateWithName("AChainNameThatIsMuchLongerThanTwentyThreeCharacters", 420420);

            var options = new AppChainBlockProductionStrategy(config).PrepareBlockOptions(1, null);

            Assert.Equal(HeaderValidator.MaximumExtraDataSize, options.ExtraData.Length);
            Assert.StartsWith("AppChain:AChainName", System.Text.Encoding.UTF8.GetString(options.ExtraData));
        }

        [Fact]
        public void Given_AShortChainName_When_ABlockIsPrepared_Then_TheExtraDataCarriesTheWholeName()
        {
            var config = AppChainConfig.CreateWithName("Short", 420420);

            var options = new AppChainBlockProductionStrategy(config).PrepareBlockOptions(1, null);

            Assert.Equal("AppChain:Short", System.Text.Encoding.UTF8.GetString(options.ExtraData));
        }

        [Fact]
        public void Given_AParentFarInThePast_When_TheNextBlockIsPrepared_Then_ItsTimestampIsTheClock()
        {
            var config = AppChainConfig.CreateWithName("Timestamps", 420420);
            var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var options = new AppChainBlockProductionStrategy(config).PrepareBlockOptions(
                2, new BlockHeader { BlockNumber = 1, GasLimit = 30_000_000, Timestamp = 1 });

            Assert.InRange((long)options.Timestamp, before, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
    }
}
