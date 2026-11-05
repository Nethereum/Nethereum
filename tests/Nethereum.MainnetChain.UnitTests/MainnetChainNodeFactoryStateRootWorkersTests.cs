using Nethereum.CoreChain.Sync;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetChainNodeFactoryStateRootWorkersTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Given_StateRootWorkers_below_one_When_the_factory_is_constructed_Then_it_is_refused(int stateRootWorkers)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MainnetChainNodeFactory(new AlwaysAcceptConsensusBlockGate(), null, null, null, 0, stateRootWorkers));

            Assert.Equal("stateRootWorkers", error.ParamName);
        }

        [Fact]
        public void Given_StateRootWorkers_of_one_When_the_factory_is_constructed_Then_it_is_accepted()
        {
            var factory = new MainnetChainNodeFactory(new AlwaysAcceptConsensusBlockGate(), null, null, null, 0, 1);

            Assert.NotNull(factory);
        }
    }
}
