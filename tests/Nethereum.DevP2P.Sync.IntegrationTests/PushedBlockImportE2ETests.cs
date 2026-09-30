using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class PushedBlockImportE2ETests
    {
        private readonly ITestOutputHelper _output;

        public PushedBlockImportE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_APeerFollowingThePushFeed_When_TheSealerProducesABlock_Then_ThePeerImportsItAndItsHeadMatches()
        {
            var (sealerNode, followerNode) = await DevChainNetwork.TwoTrustedClusterNodesAsync();
            DevChainFollower follower = null;
            try
            {
                var producer = await DevChainProducer.AttachAsync(sealerNode);
                follower = await DevChainFollower.AttachAsync(followerNode);
                await follower.StartAsync();

                var produced = await producer.ProduceAsync();
                Assert.NotNull(produced.BlockHash);
                _output.WriteLine($"sealer produced block {produced.Header.BlockNumber} hash=0x{produced.BlockHash.ToHex()}");

                var imported = await DevChainNetwork.WaitUntilAsync(
                    async () => await follower.HeightAsync() >= (long)produced.Header.BlockNumber,
                    TimeSpan.FromSeconds(30));

                Assert.True(imported,
                    $"follower did not import the pushed block (height={await follower.HeightAsync()}, " +
                    $"queued={followerNode.PushedBlocks.PendingCount}, observed={followerNode.ObservedBlocks.Count})");

                var followerHash = await followerNode.Bundle.Blocks.GetHashByNumberAsync((long)produced.Header.BlockNumber);
                Assert.True(ByteUtil.AreEqual(followerHash, produced.BlockHash),
                    "follower imported a different block than the sealer produced");

                _output.WriteLine($"follower imported block {produced.Header.BlockNumber}, head hash matches");
            }
            finally
            {
                if (follower != null) await follower.DisposeAsync();
                await sealerNode.DisposeAsync();
                await followerNode.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_APeerFollowingThePushFeed_When_TheSealerProducesSeveralBlocks_Then_ThePeerImportsThemAllInOrder()
        {
            var (sealerNode, followerNode) = await DevChainNetwork.TwoTrustedClusterNodesAsync();
            DevChainFollower follower = null;
            try
            {
                var producer = await DevChainProducer.AttachAsync(sealerNode);
                follower = await DevChainFollower.AttachAsync(followerNode);
                await follower.StartAsync();

                const int blocks = 3;
                for (var i = 0; i < blocks; i++) await producer.ProduceAsync();

                var sealerHeight = await producer.HeightAsync();
                var caughtUp = await DevChainNetwork.WaitUntilAsync(
                    async () => await follower.HeightAsync() >= sealerHeight,
                    TimeSpan.FromSeconds(45));

                Assert.True(caughtUp,
                    $"follower stalled at height {await follower.HeightAsync()} of {sealerHeight} " +
                    $"(queued={followerNode.PushedBlocks.PendingCount})");

                for (var number = 1; number <= (int)sealerHeight; number++)
                {
                    var sealerHash = await sealerNode.Bundle.Blocks.GetHashByNumberAsync(number);
                    var followerHash = await followerNode.Bundle.Blocks.GetHashByNumberAsync(number);
                    Assert.True(ByteUtil.AreEqual(sealerHash, followerHash),
                        $"block {number} differs between sealer and follower");
                }

                _output.WriteLine($"follower imported {sealerHeight} block(s), every hash matches the sealer");
            }
            finally
            {
                if (follower != null) await follower.DisposeAsync();
                await sealerNode.DisposeAsync();
                await followerNode.DisposeAsync();
            }
        }
    }
}
