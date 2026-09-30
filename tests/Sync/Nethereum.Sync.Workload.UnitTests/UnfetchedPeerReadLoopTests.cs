using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class UnfetchedPeerReadLoopTests
    {
        [Fact]
        public async Task UnfetchedPeer_ServerIdleDisconnect_ClientDetectsWithoutAnyRequest()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 20);
            await new WorkloadV1().BuildAsync(sequencer);
            await using var server = await WireServerNode.StartAsync(sequencer, idleTimeout: TimeSpan.FromSeconds(1));

            var session = await SyncPeerSession.ConnectAsync(
                server.Enode, TimeSpan.FromSeconds(10), CancellationToken.None,
                server.GenesisHash, server.NetworkId, minPeerLatestBlock: 0);

            try
            {
                var disconnected = 0;
                session.Connection.Disconnected += (_, __) => Interlocked.Exchange(ref disconnected, 1);

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                while (DateTime.UtcNow < deadline && Volatile.Read(ref disconnected) == 0)
                    await Task.Delay(100);

                Assert.Equal(1, Volatile.Read(ref disconnected));
                Assert.False(session.Connection.IsConnected,
                    "an un-fetched peer (no SendRequestAsync ever issued) must still be actively read, " +
                    "so a server-initiated idle disconnect is observed promptly");
            }
            finally
            {
                session.Dispose();
            }
        }
    }
}
