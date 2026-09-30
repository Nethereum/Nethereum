using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class PeerPoolTransportLivenessRealLoopbackTests
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
        public async Task RealPeer_NoFetchEver_StaysActive_PastOldGraceWindows_BecauseTransportKeepsReceivingKeepalivePongs()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 3);
            await new WorkloadV1().BuildAsync(sequencer);

            await using var serverA = await WireServerNode.StartAsync(sequencer, idleTimeout: TimeSpan.FromMinutes(10));
            await using var serverB = await WireServerNode.StartAsync(sequencer, idleTimeout: TimeSpan.FromMinutes(10));
            await using var serverC = await WireServerNode.StartAsync(sequencer, idleTimeout: TimeSpan.FromMinutes(10));
            await using var quietServer = await WireServerNode.StartAsync(sequencer, idleTimeout: TimeSpan.FromMinutes(10));

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(serverA.GenesisHash, serverA.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 4, MinPeerLatestBlock: 0));

            var removedEnodes = new ConcurrentBag<string>();
            pool.PeerRemoved += (_, p) => removedEnodes.Add(p.Enode);

            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(serverA.Enode);
            pool.EnqueueCandidate(serverB.Enode);
            pool.EnqueueCandidate(serverC.Enode);
            pool.EnqueueCandidate(quietServer.Enode);

            var allConnected = await WaitUntilAsync(() => pool.ActivePeers.Count == 4, TimeSpan.FromSeconds(20));
            Assert.True(allConnected, "follower did not establish all four peers over loopback RLPx");

            var wireEnodes = new[] { serverA.Enode, serverB.Enode, serverC.Enode, quietServer.Enode };
            var fetchSuccessTrackedEnodes = new[] { serverA.Enode, serverB.Enode, serverC.Enode };
            var wireTrafficCts = new CancellationTokenSource();
            var wireTrafficTask = Task.Run(async () =>
            {
                while (!wireTrafficCts.IsCancellationRequested)
                {
                    foreach (var enode in wireEnodes)
                    {
                        var peer = pool.ActivePeers.FirstOrDefault(
                            p => string.Equals(p.Enode, enode, StringComparison.OrdinalIgnoreCase))
                            as SyncPeerSession;
                        if (peer == null) continue;
                        try { await peer.GetHeadersAsync(0, 1, wireTrafficCts.Token); }
                        catch (OperationCanceledException) { }
                        catch { }
                        if (Array.IndexOf(fetchSuccessTrackedEnodes, enode) >= 0)
                            pool.ReportSuccess(peer.Id);
                    }
                    try { await Task.Delay(TimeSpan.FromSeconds(10), wireTrafficCts.Token); }
                    catch (OperationCanceledException) { }
                }
            });

            try
            {
                var pastNewPeerAndResponsiveGraceWindows = TimeSpan.FromSeconds(135);
                await Task.Delay(pastNewPeerAndResponsiveGraceWindows);
            }
            finally
            {
                wireTrafficCts.Cancel();
                await wireTrafficTask;
            }

            Assert.DoesNotContain(quietServer.Enode, removedEnodes);
            Assert.Contains(pool.ActivePeers, p => string.Equals(p.Enode, quietServer.Enode, StringComparison.OrdinalIgnoreCase));
        }
    }
}
