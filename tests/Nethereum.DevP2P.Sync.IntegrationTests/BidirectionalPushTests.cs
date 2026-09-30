using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class BidirectionalPushTests
    {
        [Fact]
        public async Task Given_ANodeWithBothInboundAndDialledPeers_When_ABlockIsProduced_Then_EveryConnectedPeerReceivesIt()
        {
            await using var sealer = DevChainNode.Create();
            await using var dialled = DevChainNode.Create();
            await using var inbound = DevChainNode.Create();

            await sealer.StartAsync();
            await dialled.StartAsync();
            await inbound.StartAsync();

            await sealer.ConnectToAsync(dialled);
            await inbound.ConnectToAsync(sealer);

            await WaitForAsync(() => sealer.BroadcastPool.Count >= 2, TimeSpan.FromSeconds(30),
                () => $"sealer registry holds {sealer.BroadcastPool.Count} sessions, expected both directions");

            var header = await SealOneBlockAsync(sealer);

            await WaitForAsync(
                () => Saw(dialled, header) && Saw(inbound, header),
                TimeSpan.FromSeconds(30),
                () => $"dialled saw {dialled.ObservedBlocks.Count}, inbound saw {inbound.ObservedBlocks.Count}");

            Assert.True(Saw(dialled, header), "the peer the sealer DIALLED never received the produced block");
            Assert.True(Saw(inbound, header), "the peer that dialled IN never received the produced block");
        }

        private static bool Saw(DevChainNode node, BlockHeader header) =>
            node.ObservedBlocks.Any(b => b.Header != null && b.Header.BlockNumber == header.BlockNumber);

        private static async Task<BlockHeader> SealOneBlockAsync(DevChainNode sealer)
        {
            var producer = await DevChainProducer.AttachAsync(sealer);
            var result = await producer.ProduceAsync();
            return result.Header;
        }

        private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout, Func<string> describe)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(50);
            }
            Assert.True(condition(), describe());
        }
    }
}
