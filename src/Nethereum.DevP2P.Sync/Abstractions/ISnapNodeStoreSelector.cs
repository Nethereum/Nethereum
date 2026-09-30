using System.Threading;
using System.Threading.Tasks;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface ISnapNodeStoreSelector
    {
        Task<ITrieNodeStore> ResolveForRootAsync(byte[] stateRoot, CancellationToken ct = default);
    }
}
