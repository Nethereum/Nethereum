using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IFetchRequestScheduler
    {
        void OnTargetRootChanged() { }

        Task<List<BlockHeader>> FetchHeadersAsync(
            ulong startBlock,
            ulong limit,
            CancellationToken ct,
            bool reverse = false);

        async Task<(List<BlockHeader> Headers, Guid PeerId)> FetchHeadersWithPeerAsync(
            ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
        {
            var headers = await FetchHeadersAsync(startBlock, limit, ct, reverse).ConfigureAwait(false);
            return (headers, Guid.Empty);
        }

        void QuarantineHeaderPeer(Guid peerId) { }

        Task<List<BlockHeader>> FetchHeadersByHashAsync(
            byte[] startHash,
            ulong limit,
            CancellationToken ct)
            => throw new NotSupportedException(
                "FetchHeadersByHashAsync is not implemented by this scheduler.");

        Task<List<BlockBody>> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes,
            CancellationToken ct);

        Task<BodyFetchResult> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes,
            IReadOnlyCollection<Guid>? excludePeers,
            CancellationToken ct);

        Task<List<List<Receipt>>> FetchReceiptsAsync(
            IReadOnlyList<byte[]> blockHashes,
            CancellationToken ct);

        Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct);

        Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct);

        /// <summary>
        /// Account-range fetch with per-peer accountability (ERC-4337-audit D2/2.16). Identical to the
        /// delegate-less overload above, except <paramref name="verifyResponse"/> is invoked on the raw
        /// response INSIDE the scheduler's per-peer worker — the one place the serving peer identity is in
        /// scope. Returning <c>false</c> flags a Merkle-proof-verification failure: the serving peer is
        /// quarantined (like the wholly-empty-response bench already applied here) and the request rotates
        /// to another snap peer. This mirrors go-ethereum, which DISCONNECTS a peer whose account-range
        /// proof fails — <c>OnAccounts</c> returns the verify error
        /// (eth/protocols/snap/sync.go:2573-2578, "Account range failed proof") and <c>Handle</c> drops the
        /// peer on any handler error (eth/protocols/snap/handler.go:113-120, "When this function terminates,
        /// the peer is disconnected"). Default implementation ignores the delegate (single-peer schedulers
        /// cannot rotate), so existing implementers need no change.
        /// </summary>
        Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, Func<AccountRangeMessage, bool> verifyResponse, CancellationToken ct)
            => FetchAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, ct);

        Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash,
            ulong responseBytes, Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct)
            => FetchStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, ct);

        Task<ByteCodesMessage> FetchByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct);

        Task<ByteCodesMessage> FetchByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, Func<ByteCodesMessage, bool> verifyResponse, CancellationToken ct)
            => FetchByteCodesAsync(codeHashes, responseBytes, ct);

        Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct);

        Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, Func<TrieNodesMessage, bool> verifyResponse, CancellationToken ct)
            => FetchTrieNodesAsync(stateRoot, paths, responseBytes, ct);

        bool IsSnapStateServing(IEthPeer peer) => true;
    }

    public interface IPeerRequestWorker
    {
        Task<List<BlockHeader>> GetHeadersAsync(
            IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct);

        Task<List<BlockHeader>> GetHeadersByHashAsync(
            IEthPeer peer, byte[] startHash, ulong limit, CancellationToken ct)
            => throw new NotSupportedException(
                "GetHeadersByHashAsync is not implemented by this worker.");

        Task<List<BlockBody>> GetBodiesAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct);

        Task<List<List<Receipt>>> GetReceiptsAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct);

        Task<AccountRangeMessage> GetAccountRangeAsync(
            IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct);

        Task<StorageRangesMessage> GetStorageRangesAsync(
            IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct);

        Task<ByteCodesMessage> GetByteCodesAsync(
            IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct);

        Task<TrieNodesMessage> GetTrieNodesAsync(
            IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct);
    }
}
