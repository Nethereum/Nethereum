using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Configuration;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeProducerConfigTests
    {
        private static AppChainServerConfig Sequencer()
        {
            var config = new AppChainServerConfig();
            config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            return config;
        }

        [Fact]
        public void Given_AProducerBlockGasLimitAndBaseFee_When_TheAppChainConfigIsMapped_Then_BothReachTheChainConfig()
        {
            var config = Sequencer();
            config.Node.Producer.BlockGasLimit = 123_456_789;
            config.Node.Producer.BaseFee = 7;

            var chain = AppChainExecutionCore.ChainConfigFor(config);

            Assert.Equal((BigInteger)123_456_789, chain.BlockGasLimit);
            Assert.Equal((BigInteger)7, chain.BaseFee);
        }

        [Fact]
        public void Given_NoProducerSection_When_TheAppChainConfigIsMapped_Then_TheChainKeepsItsOwnDefaults()
        {
            var config = Sequencer();
            var untouched = Nethereum.AppChain.AppChainConfig.CreateWithName(config.ChainName, config.ChainId);
            untouched.ForkSchedule = config.ForkSchedule;

            var chain = AppChainExecutionCore.ChainConfigFor(config);

            Assert.Equal(untouched.BlockGasLimit, chain.BlockGasLimit);
            Assert.Equal(untouched.BaseFee, chain.BaseFee);
        }

        [Fact]
        public void Given_AConfigurationSectionWithNodeProducerKeys_When_BoundToTheAppChainConfig_Then_TheyReachTheChainConfig()
        {
            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AppChain:Node:Producer:BlockGasLimit"] = "98765432",
                    ["AppChain:Node:Producer:BaseFee"] = "5",
                })
                .Build()
                .GetSection("AppChain");
            var config = Sequencer();

            section.Bind(config);
            var chain = AppChainExecutionCore.ChainConfigFor(config);

            Assert.Equal((BigInteger)98_765_432, chain.BlockGasLimit);
            Assert.Equal((BigInteger)5, chain.BaseFee);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Given_ANonPositiveProducerBlockGasLimit_When_TheAppChainConfigIsValidated_Then_ItRefuses(long limit)
        {
            var config = Sequencer();
            config.Node.Producer.BlockGasLimit = limit;

            var ex = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.Contains("AppChain:Node:Producer:BlockGasLimit must be positive", ex.Message);
        }

        [Fact]
        public void Given_APositiveProducerBlockGasLimit_When_TheAppChainConfigIsValidated_Then_ItIsAccepted()
        {
            var config = Sequencer();
            config.Node.Producer.BlockGasLimit = 1;

            config.Validate();
        }
    }
}
