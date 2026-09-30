using System.Reflection;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockExecutorSlotNumberSourceTests
    {
        private static BlockContext InvokeBuildBlockContext(BlockHeader header, ChainConfig chainConfig)
        {
            var method = typeof(BlockExecutor).GetMethod(
                "BuildBlockContext", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(BlockHeader), typeof(ChainConfig), typeof(System.Func<BlockHeader, string>) }, null);
            Assert.NotNull(method);
            return (BlockContext)method.Invoke(null, new object[] { header, chainConfig, null });
        }

        private static BlockHeader HeaderWithSlotNumber(ulong slotNumber) => new BlockHeader
        {
            BlockNumber = 1,
            Timestamp = 1704067200,
            Coinbase = "0x1111111111111111111111111111111111111111",
            GasLimit = 30_000_000,
            Difficulty = 0,
            SlotNumber = slotNumber
        };

        private static ChainConfig BuildChainConfig() => new ChainConfig
        {
            ChainId = 1,
            BlockGasLimit = 30_000_000,
            BaseFee = 0,
            Hardfork = "Amsterdam"
        };

        [Fact]
        public void BuildBlockContext_reads_the_headers_own_SlotNumber_not_a_constant()
        {
            var chainConfig = BuildChainConfig();

            var context100 = InvokeBuildBlockContext(HeaderWithSlotNumber(100), chainConfig);
            var context101 = InvokeBuildBlockContext(HeaderWithSlotNumber(101), chainConfig);

            Assert.Equal(100UL, context100.SlotNumber);
            Assert.Equal(101UL, context101.SlotNumber);
            Assert.NotEqual(context100.SlotNumber, context101.SlotNumber);
        }

        [Fact]
        public void BuildBlockContext_propagates_a_null_header_SlotNumber_as_null_not_a_default()
        {
            var header = HeaderWithSlotNumber(0);
            header.SlotNumber = null;
            var chainConfig = BuildChainConfig();

            var context = InvokeBuildBlockContext(header, chainConfig);

            Assert.Null(context.SlotNumber);
        }
    }
}
