using System.Numerics;
using Nethereum.AppChain;
using Nethereum.EVM;
using Nethereum.EVM.Gas;
using Xunit;

namespace Nethereum.AppChain.UnitTests
{
    public class AppChainConfigForkTests
    {
        private static long DeploySized =>
            GasConstants.BlockGasLimitLargeEnoughToDeployAt(stateGasActive: true);

        [Fact]
        public void Given_AnAppChainRunningAmsterdam_When_ItsBlockGasLimitIsRead_Then_ItIsLargeEnoughToDeploy()
        {
            var config = AppChainConfig.CreateWithName("demo", 420420);
            config.Hardfork = "amsterdam";

            Assert.Equal((BigInteger)DeploySized, config.BlockGasLimit);
        }

        [Fact]
        public void Given_AnAppChainRunningPrague_When_ItsBlockGasLimitIsRead_Then_ItIsThePlainExecutionHeadroom()
        {
            var config = AppChainConfig.CreateWithName("demo", 420420);
            config.Hardfork = "prague";

            Assert.Equal((BigInteger)GasConstants.BLOCK_EXECUTION_GAS_HEADROOM, config.BlockGasLimit);
        }

        [Fact]
        public void Given_AServerScheduleStatingAmsterdam_When_ItIsGivenToAnAppChainConfig_Then_TheConfigRunsAmsterdam()
        {
            var serverSchedule = ChainForkSchedule.Running(420420, HardforkName.Amsterdam);

            var config = AppChainConfig.CreateWithName("demo", 420420);
            config.ForkSchedule = serverSchedule;

            Assert.Equal(HardforkName.Amsterdam, config.PinnedFork);
            Assert.Equal(HardforkName.Amsterdam, config.NewestForkThisChainRuns);
        }

        [Fact]
        public void Given_AnAppChainConfig_When_ItStatesNoFork_Then_ItDoesNotSilentlyDefaultToOneThatCannotDeploy()
        {
            var config = AppChainConfig.CreateWithName("demo", 420420);
            config.Hardfork = "amsterdam";

            Assert.NotEqual((BigInteger)GasConstants.BLOCK_EXECUTION_GAS_HEADROOM, config.BlockGasLimit);
        }
    }
}
