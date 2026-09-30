using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolTrustedEnodeIdentityTests
    {
        private const string TrustedPubkeyLower =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string OtherPubkeyLower =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private static PeerPoolManager BuildPool(params string[] trustedDialKeys)
            => new PeerPoolManager(
                new NeverCalledHandshakeWorker(),
                new PeerPoolOptions(TargetPeerCount: 1),
                bootnodes: Array.Empty<string>(),
                trustedDialKeys: trustedDialKeys);

        [Fact]
        public void SameNodeId_UnderCanonicalForm_IsTrusted()
        {
            var canonical = $"enode://{TrustedPubkeyLower}@127.0.0.1:30303";
            var pool = BuildPool(canonical);

            Assert.True(pool.IsTrustedEnode(canonical));
        }

        [Fact]
        public void SameNodeId_UnderUppercasePubkeyForm_IsStillTrusted()
        {
            var canonical = $"enode://{TrustedPubkeyLower}@127.0.0.1:30303";
            var upper = $"enode://{TrustedPubkeyLower.ToUpperInvariant()}@127.0.0.1:30303";
            var pool = BuildPool(canonical);

            Assert.True(pool.IsTrustedEnode(upper));
        }

        [Fact]
        public void SameNodeId_UnderDifferentHostForm_IsStillTrusted()
        {
            var canonical = $"enode://{TrustedPubkeyLower}@127.0.0.1:30303";
            var differentHost = $"enode://{TrustedPubkeyLower}@localhost:30303";
            var pool = BuildPool(canonical);

            Assert.True(pool.IsTrustedEnode(differentHost));
        }

        [Fact]
        public void DifferentNodeId_IsNotTrusted()
        {
            var canonical = $"enode://{TrustedPubkeyLower}@127.0.0.1:30303";
            var other = $"enode://{OtherPubkeyLower}@127.0.0.1:30303";
            var pool = BuildPool(canonical);

            Assert.False(pool.IsTrustedEnode(other));
        }

        [Fact]
        public void MalformedEnode_IsNotTrusted_AndDoesNotThrow()
        {
            var canonical = $"enode://{TrustedPubkeyLower}@127.0.0.1:30303";
            var pool = BuildPool(canonical);

            Assert.False(pool.IsTrustedEnode("not-an-enode"));
            Assert.False(pool.IsTrustedEnode(string.Empty));
            Assert.False(pool.IsTrustedEnode(null!));
        }

        [Fact]
        public void NoTrustedKeysConfigured_NothingIsTrusted()
        {
            var canonical = $"enode://{TrustedPubkeyLower}@127.0.0.1:30303";
            var pool = BuildPool();

            Assert.False(pool.IsTrustedEnode(canonical));
        }

        private sealed class NeverCalledHandshakeWorker : IPeerHandshakeWorker
        {
            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => throw new InvalidOperationException("not expected to be called in this test");
        }
    }
}
