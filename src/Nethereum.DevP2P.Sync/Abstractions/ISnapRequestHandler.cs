using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface ISnapRequestHandler
    {
        Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default);
        Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default);
        Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default);
        Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default);
        Task<BlockAccessListsMessage> GetBlockAccessListsAsync(GetBlockAccessListsMessage request, CancellationToken ct = default);
    }
}
