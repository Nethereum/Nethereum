using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.ModelFactories;
using Xunit;

namespace Nethereum.RPC.UnitTests.InterceptorTests
{
    public class BlockHeaderFactoryTests
    {
        [Fact]
        public void Should_TrimMixHashRlpPrefix()
        {
            var bytes = "0xa0ed5b0ca9b4a8f00aaf9b4b3b9ede7d20b0cab7f085e1e5a51553af9a7886c252".HexToByteArray();
            var result = BlockHeaderRPCFactory.EnsureMixHashWithoutRLPSizePrefix(bytes);
            Assert.True(result.Length == 32);
        }

        [Fact]
        public void Should_TrimNonceRlpPrefix()
        {
            var bytes = "0x88c9a86158ff0cea0e".HexToByteArray();
            var result = BlockHeaderRPCFactory.EnsureNonceWithoutRLPSizePrefix(bytes);
            Assert.True(result.Length == 8);
        }
    }
}