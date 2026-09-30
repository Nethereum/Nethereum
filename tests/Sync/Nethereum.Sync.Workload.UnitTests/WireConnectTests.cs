using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Chain.TestData.Vectors;
using Xunit;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class WireConnectTests
    {
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(100);
            }
            return condition();
        }

        [Fact]
        public async Task TwoNodes_ConnectOverLoopbackRlpx_FollowerSeesSnapPeer()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 20);
            await new WorkloadV1().BuildAsync(sequencer);
            await using var server = await WireServerNode.StartAsync(sequencer);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);

            var connected = await WaitUntilAsync(
                () => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap),
                TimeSpan.FromSeconds(20));

            Assert.True(connected, "follower did not establish a snap-capable peer over loopback RLPx");
        }
    }
}
