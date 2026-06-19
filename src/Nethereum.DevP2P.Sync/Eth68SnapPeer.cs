using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// <see cref="ISnapPeer"/> adapter over a live <see cref="MainnetPeerSession"/>
    /// with snap/1 negotiated alongside eth/68 (or eth/69). Forwards each
    /// typed snap request into the wire-level call on the session, which
    /// handles request-id pairing, response timeout, and snap-capability
    /// presence checks.
    ///
    /// <para>
    /// Used by <c>SnapBootstrapper</c> to drive a snap/1 fetch against the
    /// trusted peer. Other consumers (in-process tests, AppChain follower)
    /// should use <see cref="InProcessSnapPeer"/> over an
    /// <see cref="ISnapRequestHandler"/> instead.
    /// </para>
    /// </summary>
    public sealed class Eth68SnapPeer : ISnapPeer
    {
        private readonly MainnetPeerSession _session;

        public Eth68SnapPeer(MainnetPeerSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(
            GetAccountRangeMessage request, CancellationToken ct = default)
            => _session.GetAccountRangeAsync(
                request.RootHash, request.StartingHash, request.LimitHash, request.ResponseBytes, ct);

        public Task<StorageRangesMessage> GetStorageRangesAsync(
            GetStorageRangesMessage request, CancellationToken ct = default)
            => _session.GetStorageRangesAsync(
                request.RootHash, request.AccountHashes,
                request.StartingHash, request.LimitHash, request.ResponseBytes, ct);

        public Task<ByteCodesMessage> GetByteCodesAsync(
            GetByteCodesMessage request, CancellationToken ct = default)
            => _session.GetByteCodesAsync(request.Hashes, request.ResponseBytes, ct);

        public Task<TrieNodesMessage> GetTrieNodesAsync(
            GetTrieNodesMessage request, CancellationToken ct = default)
            => _session.GetTrieNodesAsync(request.RootHash, request.Paths, request.ResponseBytes, ct);
    }
}
