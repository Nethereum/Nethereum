using System.Threading.Tasks;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainAmsterdamSlotNumberTests
    {
        private static DevChainConfig AtAmsterdam() =>
            new DevChainConfig { Hardfork = "amsterdam" };

        [Fact]
        public async Task Given_DevChainAtAmsterdam_When_BlockProduced_Then_SlotNumberIsTheBlockNumber()
        {
            using var node = new DevChainNode(AtAmsterdam());
            await node.StartAsync();

            await node.MineBlockAsync();

            var block = await node.GetBlockByNumberAsync(1);
            Assert.NotNull(block);
            Assert.Equal(1, block.BlockNumber);
            Assert.Equal((ulong)1, block.SlotNumber);
        }

        [Fact]
        public async Task Given_DevChainAtAmsterdam_When_SeveralBlocksProduced_Then_TheSlotAdvancesWithThem()
        {
            using var node = new DevChainNode(AtAmsterdam());
            await node.StartAsync();

            await node.MineBlockAsync();
            await node.MineBlockAsync();
            await node.MineBlockAsync();

            for (var n = 1; n <= 3; n++)
            {
                var block = await node.GetBlockByNumberAsync(n);
                Assert.NotNull(block);
                Assert.Equal((ulong)n, block.SlotNumber);
            }
        }

        [Fact]
        public async Task Given_DevChainBeforeAmsterdam_When_BlockProduced_Then_ThereIsNoSlotNumber()
        {
            using var node = new DevChainNode(new DevChainConfig { Hardfork = "prague" });
            await node.StartAsync();

            await node.MineBlockAsync();

            var block = await node.GetBlockByNumberAsync(1);
            Assert.NotNull(block);
            Assert.Null(block.SlotNumber);
        }
    }
}
