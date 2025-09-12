using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.UI.Components.Abstractions
{
    public interface INetworkProviderService
    {
        Task<List<ChainFeature>> GetDefaultNetworksAsync();
    }
}
