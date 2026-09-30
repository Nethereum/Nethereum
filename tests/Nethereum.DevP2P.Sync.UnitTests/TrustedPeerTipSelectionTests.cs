using System;
using System.Collections.Generic;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TrustedPeerTipSelectionTests
    {
        [Fact]
        public void Given_AnUntrustedPeerAheadOfATrustedOne_When_TheTipPeerIsChosen_Then_TheTrustedPeerWins()
        {
            var peers = new List<IEthPeer>
            {
                Peer("trusted", latestBlock: 10, trusted: true),
                Peer("stranger", latestBlock: 9_999, trusted: false)
            };

            var chosen = PeerHeadCanonicalSource.SelectHeadPeer(peers, trustedPeersOnly: true);

            Assert.Equal("trusted", chosen.Enode);
        }

        [Fact]
        public void Given_OnlyUntrustedPeers_When_TheTipPeerIsChosen_Then_ThereIsNoTipRatherThanAnUntrustedOne()
        {
            var peers = new List<IEthPeer> { Peer("stranger", latestBlock: 9_999, trusted: false) };

            var chosen = PeerHeadCanonicalSource.SelectHeadPeer(peers, trustedPeersOnly: true);

            Assert.Null(chosen);
        }

        [Fact]
        public void Given_SeveralTrustedPeers_When_TheTipPeerIsChosen_Then_TheHighestTrustedHeadWins()
        {
            var peers = new List<IEthPeer>
            {
                Peer("behind", latestBlock: 10, trusted: true),
                Peer("ahead", latestBlock: 42, trusted: true)
            };

            var chosen = PeerHeadCanonicalSource.SelectHeadPeer(peers, trustedPeersOnly: true);

            Assert.Equal("ahead", chosen.Enode);
        }

        [Fact]
        public void Given_TrustIsNotRequired_When_TheTipPeerIsChosen_Then_TheHighestHeadWinsRegardlessOfTrust()
        {
            var peers = new List<IEthPeer>
            {
                Peer("trusted", latestBlock: 10, trusted: true),
                Peer("stranger", latestBlock: 9_999, trusted: false)
            };

            var chosen = PeerHeadCanonicalSource.SelectHeadPeer(peers, trustedPeersOnly: false);

            Assert.Equal("stranger", chosen.Enode);
        }

        private static IEthPeer Peer(string enode, ulong latestBlock, bool trusted) =>
            new StubPeer(enode, latestBlock, trusted);

        private sealed class StubPeer : IEthPeer
        {
            public StubPeer(string enode, ulong latestBlock, bool trusted)
            {
                Enode = enode;
                PeerLatestBlock = latestBlock;
                IsTrusted = trusted;
            }

            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host => "127.0.0.1";
            public bool IsTrusted { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock { get; }
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null;

#pragma warning disable 67
            public event EventHandler<IEthPeer> Disconnected;
#pragma warning restore 67
        }
    }
}
