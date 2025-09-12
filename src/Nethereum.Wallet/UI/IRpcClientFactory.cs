using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Chain;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface IRpcClientFactory
    {
        Task<IClient> CreateClientAsync(ChainFeature? activeChain = null);
    }

}
