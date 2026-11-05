using System;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainBlockTimestampTests
    {
        [Fact]
        public async Task Given_AParentStampedAheadOfTheClock_When_TheNextBlockIsMined_Then_ItsTimestampIsTheParentsPlusOne()
        {
            using var node = new DevChainNode();
            await node.StartAsync();
            var ahead = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1_000;
            node.DevConfig.SetNextBlockTimestamp(ahead);
            await node.MineBlockAsync();

            await node.MineBlockAsync();

            var second = await node.GetBlockByNumberAsync(2);
            Assert.Equal(ahead + 1, second.Timestamp);
        }

        [Fact]
        public async Task Given_ParentsMinedInTheSameSecond_When_BlocksAreMined_Then_EveryTimestampIsStrictlyAfterItsParents()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            for (var i = 0; i < 5; i++) await node.MineBlockAsync();

            for (var number = 1; number <= 5; number++)
            {
                var block = await node.GetBlockByNumberAsync(number);
                var parent = await node.GetBlockByNumberAsync(number - 1);
                Assert.True(block.Timestamp > parent.Timestamp, $"block {number} timestamp {block.Timestamp} is not after {parent.Timestamp}");
            }
        }

        [Fact]
        public async Task Given_ANextBlockTimestampOverride_When_TheBlockIsMined_Then_TheOverrideIsHonoured()
        {
            using var node = new DevChainNode();
            await node.StartAsync();
            var chosen = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 50;
            node.DevConfig.SetNextBlockTimestamp(chosen);

            await node.MineBlockAsync();

            Assert.Equal(chosen, (await node.GetBlockByNumberAsync(1)).Timestamp);
        }
    }
}
