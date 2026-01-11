using System.Numerics;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class BlockContextTests
    {
        [Fact]
        public void FromConfig_ShouldCreateCorrectContext()
        {
            var config = new DevChainConfig
            {
                ChainId = 1337,
                Coinbase = "0x1234567890123456789012345678901234567890",
                BlockGasLimit = 30_000_000,
                BaseFee = 1_000_000_000
            };

            var context = BlockContext.FromConfig(config, 100, 1609459200);

            Assert.Equal(100, context.BlockNumber);
            Assert.Equal(1609459200, context.Timestamp);
            Assert.Equal("0x1234567890123456789012345678901234567890", context.Coinbase);
            Assert.Equal(30_000_000, context.GasLimit);
            Assert.Equal(1_000_000_000, context.BaseFee);
            Assert.Equal(1337, context.ChainId);
            Assert.Equal(1, context.Difficulty);
            Assert.Equal(32, context.PrevRandao.Length);
        }

        [Fact]
        public void Difficulty_ShouldDefaultToOne()
        {
            var context = new BlockContext();

            Assert.Equal(1, context.Difficulty);
        }

        [Fact]
        public void PrevRandao_CanBeSet()
        {
            var randao = new byte[32];
            for (int i = 0; i < 32; i++) randao[i] = (byte)i;

            var context = new BlockContext
            {
                PrevRandao = randao
            };

            Assert.Equal(randao, context.PrevRandao);
        }
    }
}
