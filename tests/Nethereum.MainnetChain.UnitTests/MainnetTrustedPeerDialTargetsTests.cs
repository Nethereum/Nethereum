using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.MainnetChain.Configuration;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetTrustedPeerDialTargetsTests
    {
        private static readonly byte[] GenesisHash = Enumerable.Range(0, 32).Select(i => (byte)(0x5a ^ i)).ToArray();

        private static string[] SixteenTrustedPeers()
        {
            var nibbles = "0123456789abcdef";
            var enodes = new string[nibbles.Length];
            for (var i = 0; i < nibbles.Length; i++)
            {
                var hostPort = i switch
                {
                    0 => "127.0.0.1:30307",
                    1 => "112.154.155.200:20001",
                    _ => $"10.0.0.{i}:30303",
                };
                enodes[i] = $"enode://{new string(nibbles[i], 128)}@{hostPort}";
            }
            return enodes;
        }

        private static ChainNodeConfig MapFromCli(params string[] args) => MapFromCli(new MainnetChainServerConfig(), args);

        private static ChainNodeConfig MapFromCli(MainnetChainServerConfig config, params string[] args)
        {
            MainnetChainCliArgs.Apply(config, args);
            var node = config.ToChainNodeConfig();
            node.Storage.InMemory = true;
            return node;
        }

        private static async Task<InMemoryChainStoreBundle> GenesisBundleAsync()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 0, Timestamp = 1000 }, GenesisHash);
            return bundle;
        }

        private static async Task<IReadOnlyCollection<string>> DialledEnodesOnceAllOfAreSeenAsync(
            ChainNodeConfig node, IReadOnlyCollection<string> expected)
        {
            var worker = new ScriptedHandshakeWorker();
            var bundle = await GenesisBundleAsync();
            var stack = await ChainNodeSyncStack.StartAsync(new ScriptedProfile(worker), bundle, node, loggerFactory: null);
            try
            {
                await WaitUntilAsync(() => expected.All(worker.Dialled.Contains));
                return worker.Dialled;
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_TheTrustedPeerFlagCarriesSixteenCommaSeparatedEnodes_When_TheMainnetConfigStartsTheSyncStack_Then_ThePoolDialsEachEnodeOnItsOwnAndNothingElse()
        {
            var trustedPeers = SixteenTrustedPeers();

            var dialled = await DialledEnodesOnceAllOfAreSeenAsync(
                MapFromCli("--trusted-peer", string.Join(",", trustedPeers)), trustedPeers);

            Assert.Equal(trustedPeers.OrderBy(e => e), dialled.OrderBy(e => e));
            Assert.All(dialled, enode => Assert.True(EnodeUrl.TryParse(enode, out _), enode));
        }

        [Fact]
        public async Task Given_TheTrustedPeerFlagSeparatesEnodesWithCommaAndSpaces_When_TheMainnetConfigStartsTheSyncStack_Then_EachEnodeIsTrimmedAndDialled()
        {
            var trustedPeers = SixteenTrustedPeers().Take(3).ToArray();

            var dialled = await DialledEnodesOnceAllOfAreSeenAsync(
                MapFromCli("--trusted-peer", $"{trustedPeers[0]}, {trustedPeers[1]} ,{trustedPeers[2]}, "), trustedPeers);

            Assert.Equal(trustedPeers.OrderBy(e => e), dialled.OrderBy(e => e));
        }

        [Fact]
        public void Given_TheTrustedPeerFlagCarriesSixteenCommaSeparatedEnodes_When_TrustedNodeIdsAreResolved_Then_EachPeerIsTrustedByItsOwnNodeIdAndNoneIsMalformed()
        {
            var trustedPeers = SixteenTrustedPeers();

            var node = MapFromCli("--trusted-peer", string.Join(",", trustedPeers));
            var nodeIds = node.ResolveTrustedNodeIds(out var malformed);

            Assert.Empty(malformed);
            Assert.Equal(trustedPeers.Select(p => EnodeUrl.Parse(p).PeerId), nodeIds);
        }

        [Fact]
        public async Task Given_ThreeTrustedPeersAndTargetPeersTwo_When_ARemoteTrustedPeerIsDroppedWhileThePoolStaysAtTarget_Then_TheTrustedKeeperRedialsAndReAddsIt()
        {
            var trustedPeers = SixteenTrustedPeers().Take(3).ToArray();
            var remote = trustedPeers[1];
            var worker = new ScriptedHandshakeWorker(connects: true);
            var bundle = await GenesisBundleAsync();
            var stack = await ChainNodeSyncStack.StartAsync(
                new ScriptedProfile(worker), bundle,
                MapFromCli(new MainnetChainServerConfig { TargetPeers = 2 }, "--trusted-peer", string.Join(",", trustedPeers)),
                loggerFactory: null);
            try
            {
                var addedRemote = new ConcurrentQueue<IEthPeer>();
                stack.Pool.PeerAdded += (_, p) => { if (p.Enode == remote) addedRemote.Enqueue(p); };
                Assert.Equal(2, stack.Pool.TargetPeerCount);
                await WaitUntilAsync(() => stack.Pool.ActivePeers.Count == 3, KeeperWindow);
                var firstSession = stack.Pool.ActivePeers.Single(p => p.Enode == remote);

                await stack.Pool.DropAsync(firstSession.Id, "unresponsive", CancellationToken.None);
                await WaitUntilAsync(() => !stack.Pool.IsPeerActive(firstSession.Id), TimeSpan.FromSeconds(5));
                Assert.Equal(stack.Pool.TargetPeerCount, stack.Pool.ActivePeers.Count);

                await WaitUntilAsync(() => addedRemote.Any(p => p.Id != firstSession.Id), KeeperWindow);
                Assert.Contains(stack.Pool.ActivePeers, p => p.Enode == remote);
                Assert.Equal(2, worker.HandshakesOf(remote));
            }
            finally
            {
                await stack.DisposeAsync();
                await bundle.DisposeAsync();
            }
        }

        private static readonly TimeSpan KeeperWindow = TimeSpan.FromSeconds(45);

        private static Task WaitUntilAsync(Func<bool> condition) => WaitUntilAsync(condition, TimeSpan.FromSeconds(20));

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan within)
        {
            var deadline = DateTime.UtcNow + within;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            Assert.True(condition(), $"condition not reached within {within.TotalSeconds}s");
        }

        private sealed class ScriptedProfile : IChainProfile
        {
            private readonly IPeerHandshakeWorker _worker;

            public ScriptedProfile(IPeerHandshakeWorker worker) => _worker = worker;

            public ulong NetworkId => 1;

            public byte[] GenesisHash => MainnetTrustedPeerDialTargetsTests.GenesisHash;

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds => (Array.Empty<ulong>(), Array.Empty<ulong>());

            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();

            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) => _worker;
        }

        private sealed class ScriptedHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, int> _handshakes = new();
            private readonly bool _connects;

            public ScriptedHandshakeWorker(bool connects = false) => _connects = connects;

            public IReadOnlyCollection<string> Dialled => _handshakes.Keys.ToArray();

            public int HandshakesOf(string enode) => _handshakes.TryGetValue(enode, out var n) ? n : 0;

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _handshakes.AddOrUpdate(enode, 1, (_, n) => n + 1);
                return _connects
                    ? Task.FromResult<IEthPeer>(new ConnectedPeer(enode))
                    : Task.FromException<IEthPeer>(new InvalidOperationException("never connects"));
            }
        }

        private sealed class ConnectedPeer : IEthPeer
        {
            public ConnectedPeer(string enode)
            {
                Enode = enode;
                Host = enode;
                Connection = new RlpxConnection(EthECKey.GenerateKey());
                Connection.Disconnected += (_, __) => Disconnected?.Invoke(this, this);
            }

            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection { get; }
            public DateTime LastFrameReceivedUtc => DateTime.UtcNow;
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}
