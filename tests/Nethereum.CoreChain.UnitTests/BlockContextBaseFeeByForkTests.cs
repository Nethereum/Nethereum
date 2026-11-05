using System.Numerics;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockContextBaseFeeByForkTests
    {
        private static BlockHeader HeaderWithoutBaseFee() => new BlockHeader
        {
            BlockNumber = 5,
            Timestamp = 1000,
            GasLimit = 30_000_000,
            Coinbase = "0x0000000000000000000000000000000000000000"
        };

        [Fact]
        public void Given_APreLondonHeaderOnAChainConfiguredWithABaseFee_When_ItsBlockContextIsBuilt_Then_TheBaseFeeIsZero()
        {
            var config = new ChainConfig { ChainId = 1, BaseFee = 1_000_000_000 };

            var context = BlockExecutor.BuildBlockContext(HeaderWithoutBaseFee(), config, HardforkName.Berlin);

            Assert.Equal(BigInteger.Zero, (BigInteger)context.BaseFee);
        }

        [Fact]
        public void Given_ALondonHeaderWithoutABaseFee_When_ItsBlockContextIsBuilt_Then_TheConfiguredBaseFeeIsUsed()
        {
            var config = new ChainConfig { ChainId = 1, BaseFee = 1_000_000_000 };

            var context = BlockExecutor.BuildBlockContext(HeaderWithoutBaseFee(), config, HardforkName.London);

            Assert.Equal(new BigInteger(1_000_000_000), (BigInteger)context.BaseFee);
        }
    }
}
