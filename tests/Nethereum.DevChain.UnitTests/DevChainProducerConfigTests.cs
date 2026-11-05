using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Configuration;
using Nethereum.DevChain.Configuration;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainProducerConfigTests
    {
        [Fact]
        public void Given_NoProducerSection_When_TheLiveChainConfigIsBuilt_Then_TheGasLimitAndBaseFeeAreTodaysDefaults()
        {
            var untouched = new DevChainServerConfig().Chain;
            var config = new DevChainServerConfig();

            var chain = config.GetLiveChainConfig();

            Assert.Equal(untouched.BlockGasLimit, chain.BlockGasLimit);
            Assert.Equal(untouched.BaseFee, chain.BaseFee);
            Assert.Equal((long)untouched.BlockGasLimit, config.BlockGasLimit);
        }

        [Fact]
        public void Given_AProducerBlockGasLimitAndBaseFee_When_TheLiveChainConfigIsBuilt_Then_BothReachIt()
        {
            var config = new DevChainServerConfig();
            config.Node.Producer.BlockGasLimit = 77_000_000;
            config.Node.Producer.BaseFee = 9;

            var chain = config.GetLiveChainConfig();

            Assert.Equal((BigInteger)77_000_000, chain.BlockGasLimit);
            Assert.Equal((BigInteger)9, chain.BaseFee);
            Assert.Equal(77_000_000, config.BlockGasLimit);
        }

        [Fact]
        public void Given_TheShippedBlockGasLimitProperty_When_ItIsSetTheWayTheGasLimitCliFlagSetsIt_Then_ItIsTheSharedProducerValueAndReachesTheChain()
        {
            var config = new DevChainServerConfig();

            config.BlockGasLimit = 55_000_000;

            Assert.Equal(55_000_000, config.Node.Producer.BlockGasLimit);
            Assert.Equal((BigInteger)55_000_000, config.GetLiveChainConfig().BlockGasLimit);
        }

        [Fact]
        public void Given_AConfigurationSectionWithNodeProducerKeys_When_BoundToTheDevChainConfig_Then_TheyReachTheChain()
        {
            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DevChain:Node:Producer:BlockGasLimit"] = "66000000",
                    ["DevChain:Node:Producer:BaseFee"] = "3",
                })
                .Build()
                .GetSection("DevChain");
            var config = new DevChainServerConfig();

            section.Bind(config);
            var chain = config.GetLiveChainConfig();

            Assert.Equal((BigInteger)66_000_000, chain.BlockGasLimit);
            Assert.Equal((BigInteger)3, chain.BaseFee);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Given_ANonPositiveProducerBlockGasLimit_When_TheDevChainConfigIsValidated_Then_ItRefuses(long limit)
        {
            var config = new DevChainServerConfig();
            config.Node.Producer.BlockGasLimit = limit;

            var ex = Assert.Throws<InvalidOperationException>(() => DevChainServerConfigValidator.Validate(config));
            Assert.Contains("DevChain:Node:Producer:BlockGasLimit must be positive", ex.Message);
        }
    }
}
