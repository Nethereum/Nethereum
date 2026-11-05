using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class GasLimitBoundTests
    {
        [Theory]
        [InlineData(20_000_000L, 20_019_530L, 19_980_470L)]
        [InlineData(40_000_000L, 40_039_061L, 39_960_939L)]
        public void Given_AParentGasLimit_When_SteppingTowardADesiredLimit_Then_ItMovesAtMostOneBoundStep(
            long parentGasLimit, long highestNext, long lowestNext)
        {
            Assert.Equal(highestNext, GasLimitBound.StepToward(parentGasLimit, 2 * parentGasLimit));
            Assert.Equal(lowestNext, GasLimitBound.StepToward(parentGasLimit, 0));
            Assert.Equal(parentGasLimit - 1, GasLimitBound.StepToward(parentGasLimit, parentGasLimit - 1));
            Assert.Equal(parentGasLimit + 1, GasLimitBound.StepToward(parentGasLimit, parentGasLimit + 1));
            Assert.Equal(parentGasLimit, GasLimitBound.StepToward(parentGasLimit, parentGasLimit));
        }
    }
}
