using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPreferenceOrderingTests
    {
        private sealed class P : IEthPeer
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Name = "";
            public string Enode => "enode://" + Name + "@127.0.0.1:30303";
            public string Host => "127.0.0.1";
            public bool IsTrusted { get; init; }
            public int EthVersion => 69;
            public ulong PeerLatestBlock => 0;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer> Disconnected { add { } remove { } }
        }

        private sealed class Pool : IPeerPool
        {
            private readonly IEthPeer[] _p;
            public Pool(params IEthPeer[] p) => _p = p;
            public IReadOnlyCollection<IEthPeer> ActivePeers => _p;
            public int TargetPeerCount => _p.Length;
            public event EventHandler<IEthPeer> PeerAdded { add { } remove { } }
            public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string e, string r, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        [Fact]
        public void ActivePeersByPreference_PutsTrustedFirst_RegardlessOfInputOrder()
        {
            var ordinaryA = new P { Name = "a" };
            var trusted = new P { Name = "geth", IsTrusted = true };
            var ordinaryB = new P { Name = "b" };
            IPeerPool pool = new Pool(ordinaryA, trusted, ordinaryB);

            var ordered = pool.ActivePeersByPreference;

            Assert.Same(trusted, ordered[0]);
            Assert.True(ordered[0].IsTrusted);
            Assert.Equal(3, ordered.Count);
            Assert.All(ordered.Skip(1), p => Assert.False(p.IsTrusted));
        }

        [Fact]
        public void ActivePeersByPreference_NoTrusted_KeepsEveryPeer()
        {
            IPeerPool pool = new Pool(new P { Name = "a" }, new P { Name = "b" });

            var ordered = pool.ActivePeersByPreference;

            Assert.Equal(2, ordered.Count);
            Assert.All(ordered, p => Assert.False(p.IsTrusted));
        }

        [Fact]
        public void OrderByPreference_TrustBeatsScore_ThenScoreDescendingWithinTier()
        {
            var trustedLowScore = new P { Name = "geth", IsTrusted = true };
            var ordinaryHighScore = new P { Name = "hi" };
            var ordinaryLowScore = new P { Name = "lo" };
            Func<string, double> scoreOf = e => e.Contains("hi") ? 100 : e.Contains("lo") ? 5 : 1;

            var ordered = Peering.PeerPoolManager.OrderByPreference(
                new IEthPeer[] { ordinaryLowScore, ordinaryHighScore, trustedLowScore }, scoreOf);

            Assert.Same(trustedLowScore, ordered[0]);
            Assert.Same(ordinaryHighScore, ordered[1]);
            Assert.Same(ordinaryLowScore, ordered[2]);
        }
    }
}
