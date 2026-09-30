using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.ChainNode.Hosting
{
    public interface IChainDefinition
    {
        Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct);

        Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle);

        ICanonicalStateRootSource CreateTip(PeerPoolManager pool);
    }
}
