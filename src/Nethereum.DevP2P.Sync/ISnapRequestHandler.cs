using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Strategy for serving snap/1 read requests from incoming peers.
    /// The four request types each map to a typed response:
    ///   GetAccountRange   -> AccountRange
    ///   GetStorageRanges  -> StorageRanges
    ///   GetByteCodes      -> ByteCodes
    ///   GetTrieNodes      -> TrieNodes
    ///
    /// Implementations:
    /// - PatriciaSnapRequestHandler: serves accounts/storage from a state trie
    ///   plus an IBytecodeStore and an IStateTrieNodeStore.
    /// - DelegatingSnapHandler (future): forwards to another snap peer.
    /// </summary>
    public interface ISnapRequestHandler
    {
        Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default);
        Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default);
        Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default);
        Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default);
    }
}
