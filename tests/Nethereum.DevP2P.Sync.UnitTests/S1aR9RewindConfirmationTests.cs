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
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class S1aR9RewindConfirmationTests
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

        private static async Task<Env> FollowerAtTheTipAsync(IFetchRequestScheduler scheduler, ulong maxRewindDepth, int roundTripMs = 0, int fallbackPollMs = 20)
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
                env.Pull, env.Pool, number => Task.FromResult(number == 0 ? Genesis : LocalHash), scheduler, maxRewindDepth,
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


        private sealed class HashLookupScheduler : IFetchRequestScheduler
        {
            private readonly List<BlockHeader> _reply;
            public int ByHashCalls;
            public readonly List<Guid> Quarantined = new List<Guid>();

            public HashLookupScheduler(List<BlockHeader> reply) { _reply = reply; }

            public Task<List<BlockHeader>> FetchHeadersByHashAsync(byte[] startHash, ulong limit, CancellationToken ct)
            {
                Interlocked.Increment(ref ByHashCalls);
                return Task.FromResult(_reply);
            }

            public void QuarantineHeaderPeer(Guid peerId) { lock (Quarantined) Quarantined.Add(peerId); }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotSupportedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotSupportedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotSupportedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotSupportedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotSupportedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotSupportedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotSupportedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotSupportedException();
        }

        private static List<BlockHeader> HeaderAt(long number, byte[] parentHash) =>
            new List<BlockHeader> { new BlockHeader { BlockNumber = number, ParentHash = parentHash } };

        private static bool PulledFrom(Env env, ulong block)
        {
            lock (env.Pull.Requested) return env.Pull.Requested.Skip(env.RequestsAtTip).Any(from => from == block);
        }

        [Fact]
        public async Task S1aR9_02_Given_AnAnnouncedHeadWithinTheRewindDepthWhoseHeaderTheNetworkConfirmsAsOursSibling_When_TheUpdateArrives_Then_TheFollowerPullsFromThatBlock()
        {
            var scheduler = new HashLookupScheduler(HeaderAt(500, LocalHash));
            await using var env = await FollowerAtTheTipAsync(scheduler, maxRewindDepth: 600);

            await env.Registry.SendToPeersAsync(
                env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate, ForgedRange(500, 0xD1));

            Assert.True(await UntilAsync(() => PulledFrom(env, 500), TimeSpan.FromSeconds(5)));
            Assert.Equal(1, scheduler.ByHashCalls);
        }

        [Fact]
        public async Task S1aR9_02_Given_AnAnnouncedHeadWhoseHashNoPeerCanServe_When_TheUpdateArrives_Then_TheFollowerDoesNotRewindAndTheAnnouncingPeerIsQuarantined()
        {
            var scheduler = new HashLookupScheduler(new List<BlockHeader>());
            await using var env = await FollowerAtTheTipAsync(scheduler, maxRewindDepth: 600);

            await env.Registry.SendToPeersAsync(
                env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate, ForgedRange(500, 0xD2));
            await UntilAsync(() => scheduler.ByHashCalls >= 1, TimeSpan.FromSeconds(5));
            await Task.Delay(500);

            Assert.False(PulledFrom(env, 500));
            lock (scheduler.Quarantined) Assert.Single(scheduler.Quarantined);
        }

        [Fact]
        public async Task S1aR9_02_Given_AFetchedHeaderWhoseParentIsNotABlockWeHold_When_TheUpdateArrives_Then_TheFollowerDoesNotRewind()
        {
            var scheduler = new HashLookupScheduler(HeaderAt(500, Enumerable.Repeat((byte)0x77, 32).ToArray()));
            await using var env = await FollowerAtTheTipAsync(scheduler, maxRewindDepth: 600);

            await env.Registry.SendToPeersAsync(
                env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate, ForgedRange(500, 0xD3));
            await UntilAsync(() => scheduler.ByHashCalls >= 1, TimeSpan.FromSeconds(5));
            await Task.Delay(500);

            Assert.False(PulledFrom(env, 500));
            lock (scheduler.Quarantined) Assert.Empty(scheduler.Quarantined);
        }

        [Fact]
        public async Task S1aR9_02_Given_AnAnnouncedHeadDeeperThanTheRewindDepth_When_TheUpdateArrives_Then_TheHeaderIsNotEvenFetchedAndTheFollowerDoesNotRewind()
        {
            var scheduler = new HashLookupScheduler(HeaderAt(3, LocalHash));
            await using var env = await FollowerAtTheTipAsync(scheduler, maxRewindDepth: ReorgWindow);

            await env.Registry.SendToPeersAsync(
                env.Registry.Peers.ToList(), EthMessageIds.BlockRangeUpdate, ForgedRange(3, 0xD4));
            await Task.Delay(800);

            Assert.Equal(0, scheduler.ByHashCalls);
            Assert.False(PulledFrom(env, 3));
        }
    }
}
