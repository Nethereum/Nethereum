using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class RecoveryE2ETests
    {
        private readonly ITestOutputHelper _output;

        public RecoveryE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_AFollowerThatStopped_When_ItRestarts_Then_ItResumesFromItsStoredHeightAndCatchesUp()
        {
            var producerNode = DevChainNode.Create();
            var followerNode = DevChainNode.Create();
            try
            {
                await producerNode.StartAsync();
                await followerNode.StartAsync();
                await followerNode.ConnectToAsync(producerNode);

                var producer = await DevChainProducer.AttachAsync(producerNode);
                for (var i = 0; i < 2; i++) await producer.ProduceAsync();

                var first = await DevChainFollower.AttachAsync(followerNode);
                await first.StartCatchUpAsync();

                var reached = await DevChainNetwork.WaitUntilAsync(
                    async () => await first.HeightAsync() >= new BigInteger(2),
                    TimeSpan.FromSeconds(60));
                Assert.True(reached, $"follower did not reach height 2 (at {await first.HeightAsync()})");

                await first.DisposeAsync();
                var heightAtStop = await followerNode.Bundle.Blocks.GetHeightAsync();
                Assert.Equal(new BigInteger(2), heightAtStop);

                for (var i = 0; i < 2; i++) await producer.ProduceAsync();
                var producerHeight = await producer.HeightAsync();

                var restarted = await DevChainFollower.AttachAsync(followerNode);
                try
                {
                    Assert.Equal(heightAtStop, await restarted.HeightAsync());

                    await restarted.StartCatchUpAsync();

                    var caughtUp = await DevChainNetwork.WaitUntilAsync(
                        async () => await restarted.HeightAsync() >= producerHeight,
                        TimeSpan.FromSeconds(60));
                    Assert.True(caughtUp,
                        $"restarted follower stalled at {await restarted.HeightAsync()} of {producerHeight}");

                    for (var number = 1; number <= (int)producerHeight; number++)
                    {
                        var producerHash = await producerNode.Bundle.Blocks.GetHashByNumberAsync(number);
                        var followerHash = await followerNode.Bundle.Blocks.GetHashByNumberAsync(number);
                        Assert.True(ByteUtil.AreEqual(producerHash, followerHash),
                            $"block {number} differs after restart");
                    }

                    _output.WriteLine($"follower resumed from {heightAtStop} and reached {producerHeight}");
                }
                finally
                {
                    await restarted.DisposeAsync();
                }
            }
            finally
            {
                await followerNode.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AClusterWhoseSealerStops_When_AnotherNodeSeals_Then_TheFollowerKeepsImporting()
        {
            var nodes = await DevChainNetwork.TrustedClusterAsync(3);
            DevChainFollower follower = null;
            try
            {
                var primary = nodes[0];
                var standby = nodes[1];
                var followerNode = nodes[2];

                var primarySealer = await DevChainProducer.AttachAsync(primary);

                var standbyFollower = await DevChainFollower.AttachAsync(standby);
                await standbyFollower.StartAsync();
                var standbySealer = await DevChainProducer.AttachAsync(standby);

                follower = await DevChainFollower.AttachAsync(followerNode);
                await follower.StartAsync();

                await primarySealer.ProduceAsync();

                var importedFirst = await DevChainNetwork.WaitUntilAsync(
                    async () => await follower.HeightAsync() >= new BigInteger(1)
                                && await standbyFollower.HeightAsync() >= new BigInteger(1),
                    TimeSpan.FromSeconds(30));
                Assert.True(importedFirst,
                    $"block 1 not imported (follower={await follower.HeightAsync()}, standby={await standbyFollower.HeightAsync()})");

                await primary.DisposeAsync();
                await standbySealer.ProduceAsync();

                var importedAfterFailover = await DevChainNetwork.WaitUntilAsync(
                    async () => await follower.HeightAsync() >= new BigInteger(2),
                    TimeSpan.FromSeconds(30));

                Assert.True(importedAfterFailover,
                    $"follower stopped importing after failover (height {await follower.HeightAsync()})");

                var standbyHash = await standby.Bundle.Blocks.GetHashByNumberAsync(2);
                var followerHash = await followerNode.Bundle.Blocks.GetHashByNumberAsync(2);
                Assert.True(ByteUtil.AreEqual(standbyHash, followerHash),
                    "follower imported a different block 2 than the standby sealed");

                await standbyFollower.DisposeAsync();
                _output.WriteLine("follower imported the standby's block after the primary stopped");
            }
            finally
            {
                if (follower != null) await follower.DisposeAsync();
                foreach (var node in nodes) await node.DisposeAsync();
            }
        }
    }
}
