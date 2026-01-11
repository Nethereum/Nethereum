using System.Numerics;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainConfigTests
    {
        [Fact]
        public void Default_ShouldHaveCorrectValues()
        {
            var config = DevChainConfig.Default;

            Assert.Equal(1337, config.ChainId);
            Assert.Equal("0x0000000000000000000000000000000000000000", config.Coinbase);
            Assert.Equal(30_000_000, config.BlockGasLimit);
            Assert.Equal(1_000_000_000, config.BaseFee);
            Assert.True(config.AutoMine);
            Assert.Equal(100, config.MaxTransactionsPerBlock);
            Assert.Equal(0, config.BlockTime);
            Assert.Equal(BigInteger.Parse("10000000000000000000000"), config.InitialBalance);
        }

        [Fact]
        public void Hardhat_ShouldHaveCorrectChainId()
        {
            var config = DevChainConfig.Hardhat;

            Assert.Equal(31337, config.ChainId);
            Assert.Equal(30_000_000, config.BlockGasLimit);
            Assert.True(config.AutoMine);
        }

        [Fact]
        public void Anvil_ShouldHaveCorrectChainId()
        {
            var config = DevChainConfig.Anvil;

            Assert.Equal(31337, config.ChainId);
            Assert.Equal(30_000_000, config.BlockGasLimit);
            Assert.True(config.AutoMine);
        }

        [Fact]
        public void CustomConfig_ShouldAllowOverrides()
        {
            var config = new DevChainConfig
            {
                ChainId = 12345,
                BlockGasLimit = 50_000_000,
                AutoMine = false,
                MaxTransactionsPerBlock = 50
            };

            Assert.Equal(12345, config.ChainId);
            Assert.Equal(50_000_000, config.BlockGasLimit);
            Assert.False(config.AutoMine);
            Assert.Equal(50, config.MaxTransactionsPerBlock);
        }
    }
}
