using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class S1aR9BlockRangeFloodReviewTests
    {
        private const ulong NetworkId = 424242;
        private const int Tip = 1000;
        private const int ReorgWindow = 64;

        private static readonly byte[] Genesis = Enumerable.Repeat((byte)0x11, 32).ToArray();
        private static readonly byte[] LocalHash = Enumerable.Repeat((byte)0xAA, 32).ToArray();

        private sealed class DirectHandshakeWorker : IPeerHandshakeWorker
        {
            public async Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => await SyncPeerSession.ConnectAsync(enode, timeout, ct, Genesis, NetworkId, minPeerLatestBlock);
        }

        private sealed class ChainOfTipBlocks : IBlockSource
        {
            public readonly List<ulong> Requested = new List<ulong>();
            public long Yielded;
            public int RoundTripMs;

            public DivergenceSignal LastChainBreak => null;

            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock, [EnumeratorCancellation] CancellationToken ct)
            {
                lock (Requested) Requested.Add(fromBlock);
                if (RoundTripMs > 0) await Task.Delay(RoundTripMs, ct);
                for (var number = fromBlock; number <= Tip; number++)
                {
                    Interlocked.Increment(ref Yielded);
                    yield return new BlockBundle(
                        new BlockHeader { BlockNumber = (long)number },
                        new List<ISignedTransaction>(), new List<BlockHeader>(), null, LocalHash);
                }
                await Task.CompletedTask;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class Env : IAsyncDisposable
        {
            public Eth68PeerPool Registry;
            public PeerListener Listener;
            public PeerPoolManager Pool;
            public ChainOfTipBlocks Pull;
            public CancellationTokenSource Cts;
            public Task Stream;
            public int RequestsAtTip;
            public long YieldedAtTip;

            public async ValueTask DisposeAsync()
            {
                Cts.Cancel();
                try { await Stream; } catch { }
                await Pool.DisposeAsync();
                await Listener.DisposeAsync();
            }
        }

        private static async Task<bool> UntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(20);
            }
            return condition();
        }

        private static byte[] ForgedRange(ulong latest, byte seed) =>
            BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage
            {
                EarliestBlock = 0,
                LatestBlock = latest,
                LatestBlockHash = Enumerable.Repeat(seed, 32).ToArray()
            });

        private static async Task<Env> FollowerAtTheTipAsync(int roundTripMs, int fallbackPollMs = 20)
        {
            var env = new Env { Registry = new Eth68PeerPool() };
            var key = EthECKey.GenerateKey();
            var status = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = NetworkId,
                TotalDifficulty = BigInteger.One,
                BestHash = Genesis,
                GenesisHash = Genesis,
                ForkHash = ForkId.ComputeHash(Genesis, Array.Empty<ulong>()),
                ForkNext = 0
            };
            env.Listener = new PeerListener(
                key, InMemoryChainStoreBundle.Open(),
                new PeerListenerOptions
                {
                    ListenPort = 0,
                    BindAddress = IPAddress.Loopback,
                    ServeSnap = false,
                    MirrorRemoteStatus = false,
                    EthPeerRegistry = env.Registry
                },
                statusTemplate: status);
            await env.Listener.StartAsync();
            var enode = $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{env.Listener.Port}";

            env.Pool = new PeerPoolManager(new DirectHandshakeWorker(), new PeerPoolOptions(TargetPeerCount: 1));
            await env.Pool.StartAsync(CancellationToken.None);
            env.Pool.EnqueueCandidate(enode);
            Assert.True(await UntilAsync(() => env.Pool.ActivePeers.Count == 1 && env.Registry.Count == 1, TimeSpan.FromSeconds(20)));

            env.Pull = new ChainOfTipBlocks();
            var source = new FollowingBlockSource(
                env.Pull, env.Pool, number => Task.FromResult(number == 0 ? Genesis : LocalHash),
                fallbackPoll: TimeSpan.FromMilliseconds(fallbackPollMs));
            env.Cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            env.Stream = Task.Run(async () =>
            {
                try { await foreach (var _ in source.StreamAsync(1, env.Cts.Token)) { } }
                catch (OperationCanceledException) { }
            });

            Assert.True(await UntilAsync(() => Interlocked.Read(ref env.Pull.Yielded) >= Tip, TimeSpan.FromSeconds(20)));
            await Task.Delay(300);
            env.Pull.RoundTripMs = roundTripMs;
            await Task.Delay(100);
            lock (env.Pull.Requested) env.RequestsAtTip = env.Pull.Requested.Count;
            env.YieldedAtTip = Interlocked.Read(ref env.Pull.Yielded);
            return env;
        }

        private static int PullsSinceTip(Env env)
        {
            lock (env.Pull.Requested) return env.Pull.Requested.Count - env.RequestsAtTip;
        }

        [Fact]
        public async Task S1aR9_02_Given_AFollowerAtTheTip_When_APeerAnnouncesAHeadFarBelowTheTipWithAHashWeDoNotHold_Then_TheFollowerDoesNotRedownloadTheChainFromThatBlock()
        {
            await using var env = await FollowerAtTheTipAsync(roundTripMs: 0);

            for (byte i = 1; i <= 5; i++)
            {
                await env.Registry.SendToPeersAsync(
                    env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate, ForgedRange(3, (byte)(0xB0 + i)));
                await Task.Delay(150);
            }
            await Task.Delay(500);

            List<ulong> deepRestarts;
            lock (env.Pull.Requested)
                deepRestarts = env.Pull.Requested.Skip(env.RequestsAtTip).Where(from => from < Tip - ReorgWindow).ToList();
            var redownloaded = Interlocked.Read(ref env.Pull.Yielded) - env.YieldedAtTip;

            Assert.True(
                deepRestarts.Count == 0,
                $"5 forged BlockRangeUpdates (latest=3, 70 bytes each) restarted the pull {deepRestarts.Count} time(s) from block {string.Join(",", deepRestarts.Distinct())}; {redownloaded} blocks re-pulled that the follower already holds");
        }

        [Fact]
        public async Task S1aR9_09_Given_AFollowerAtTheTip_When_APeerAnnouncesASiblingOfOurHeadAtTheSameHeight_Then_TheFollowerPullsThatBlockAgain()
        {
            await using var env = await FollowerAtTheTipAsync(roundTripMs: 0);

            await env.Registry.SendToPeersAsync(
                env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate, ForgedRange(Tip, 0xC3));

            var pulledTheSibling = await UntilAsync(() =>
            {
                lock (env.Pull.Requested) return env.Pull.Requested.Skip(env.RequestsAtTip).Any(from => from == Tip);
            }, TimeSpan.FromSeconds(5));

            Assert.True(pulledTheSibling, "an announced head that differs from ours at an equal height must be pulled from that height");
        }

        [Fact]
        public async Task S1aR9_10_Given_AFollowerAtTheTipWithALongFallbackPoll_When_APeerAnnouncesAHeadAboveOurs_Then_TheFollowerPullsAtOnceInsteadOfWaitingForThePoll()
        {
            await using var env = await FollowerAtTheTipAsync(roundTripMs: 0, fallbackPollMs: 30000);

            await env.Registry.SendToPeersAsync(
                env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate, ForgedRange(Tip + 1, 0xC4));

            var woke = await UntilAsync(() => PullsSinceTip(env) >= 1, TimeSpan.FromSeconds(3));

            Assert.True(woke, "the follower did not pull within 3 s of a head announcement with a 30 s fallback poll");
        }

        [Fact]
        public async Task S1aR9_03_Given_AFollowerAtTheTip_When_APeerFloodsBlockRangeUpdatesWithAdvancingHeads_Then_TheFollowerCoalescesThemIntoAHandfulOfPullsInsteadOfOnePerFrame()
        {
            const int frames = 300;
            await using var env = await FollowerAtTheTipAsync(roundTripMs: 20, fallbackPollMs: 30000);

            for (var i = 1; i <= frames; i++)
            {
                await env.Registry.SendToPeersAsync(
                    env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate,
                    ForgedRange((ulong)(Tip + i), (byte)(i % 250 + 1)));
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
            var pullsRightAfterTheFlood = PullsSinceTip(env);
            await Task.Delay(TimeSpan.FromSeconds(9));
            var pullsLater = PullsSinceTip(env);

            Assert.True(
                pullsLater <= 60,
                $"{frames} BlockRangeUpdate frames: {pullsRightAfterTheFlood} pulls 1 s after the flood, {pullsLater} 10 s after it (about 20 ms each); a counting wake-up queues one pull per frame");
        }
    }
}
