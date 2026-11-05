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

        void ClearStatelessMarks() { }

        Task<List<BlockHeader>> FetchHeadersAsync(
            ulong startBlock,
            ulong limit,
            CancellationToken ct,
            bool reverse = false);

        Task<List<BlockHeader>> FetchAnnouncedHeadersAsync(
            ulong startBlock,
            ulong limit,
            CancellationToken ct)
            => FetchHeadersAsync(startBlock, limit, ct);

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

        bool RegisterTimeoutForQuarantine(Guid peerId, Scheduling.TimeoutStreakKind kind, bool isTrusted, out int triggeredAtCount)
        {
            triggeredAtCount = 0;
            return false;
        }

        void RegisterSuccessForQuarantine(Guid peerId, Scheduling.TimeoutStreakKind kind) { }
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

        Task<List<List<Receipt>>> GetReceiptsAsync(
            IEthPeer peer, IReadOnlyList<byte[]> blockHashes, IReadOnlyList<ulong> gasUsedByBlock, CancellationToken ct)
            => GetReceiptsAsync(peer, blockHashes, ct);

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
