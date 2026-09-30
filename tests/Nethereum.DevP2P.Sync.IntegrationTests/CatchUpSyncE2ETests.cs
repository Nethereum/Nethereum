using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class CatchUpSyncE2ETests
    {
        private readonly ITestOutputHelper _output;

        public CatchUpSyncE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_AChainAlreadyAtHeight_When_ANewNodeEnrolsAndSyncs_Then_ItImportsEveryBlockAndMatchesTheProducer()
        {
            var producerNode = DevChainNode.Create();
            var joiner = DevChainNode.Create();
            DevChainFollower catchUp = null;
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);

                const int sealed_ = 3;
                for (var i = 0; i < sealed_; i++) await producer.ProduceAsync();
                var producerHeight = await producer.HeightAsync();
                Assert.Equal(new BigInteger(sealed_), producerHeight);

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                catchUp = await DevChainFollower.AttachAsync(joiner);
                Assert.Equal(BigInteger.Zero, await catchUp.HeightAsync());

                await catchUp.StartCatchUpAsync();

                var synced = await DevChainNetwork.WaitUntilAsync(
                    async () => await catchUp.HeightAsync() >= producerHeight,
                    TimeSpan.FromSeconds(60));

                Assert.True(synced,
                    $"joiner stalled at height {await catchUp.HeightAsync()} of {producerHeight}");

                for (var number = 1; number <= (int)producerHeight; number++)
                {
                    var producerHash = await producerNode.Bundle.Blocks.GetHashByNumberAsync(number);
                    var joinerHash = await joiner.Bundle.Blocks.GetHashByNumberAsync(number);
                    Assert.True(ByteUtil.AreEqual(producerHash, joinerHash),
                        $"block {number} differs between producer and joiner");
                }

                _output.WriteLine($"joiner synced {producerHeight} block(s) from genesis, every hash matches");
            }
            finally
            {
                if (catchUp != null) await catchUp.DisposeAsync();
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ANodeStillSyncing_When_TheProducerSealsMoreBlocks_Then_ItKeepsUpWithoutRestarting()
        {
            var producerNode = DevChainNode.Create();
            var joiner = DevChainNode.Create();
            DevChainFollower catchUp = null;
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);
                for (var i = 0; i < 2; i++) await producer.ProduceAsync();

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);
                catchUp = await DevChainFollower.AttachAsync(joiner);
                await catchUp.StartCatchUpAsync();

                var reachedFirst = await DevChainNetwork.WaitUntilAsync(
                    async () => await catchUp.HeightAsync() >= new BigInteger(2),
                    TimeSpan.FromSeconds(60));
                Assert.True(reachedFirst, $"joiner did not reach the initial head (at {await catchUp.HeightAsync()})");

                for (var i = 0; i < 2; i++) await producer.ProduceAsync();
                var finalHeight = await producer.HeightAsync();

                var keptUp = await DevChainNetwork.WaitUntilAsync(
                    async () => await catchUp.HeightAsync() >= finalHeight,
                    TimeSpan.FromSeconds(60));

                Assert.True(keptUp,
                    $"joiner stopped at {await catchUp.HeightAsync()} and did not follow to {finalHeight}");

                _output.WriteLine($"joiner caught up and kept following to height {finalHeight}");
            }
            finally
            {
                if (catchUp != null) await catchUp.DisposeAsync();
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }
    }
}
