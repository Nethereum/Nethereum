using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.HostWallet
{
    /// <summary>
    /// The wallet_addEthereumChain RPC method allows Ethereum applications (“dapps”) to suggest chains to be added to the user’s wallet application. The caller must specify a chain ID and some chain metadata. The wallet application may arbitrarily refuse or accept the request. null is returned if the chain was added, and an error otherwise.
    /// https://eips.ethereum.org/EIPS/eip-3085
    /// </summary>
    public interface IWalletAddEthereumChain
    {
        RpcRequest BuildRequest(AddEthereumChainParameter addEthereumChainParameter, object id = null);

#if !DOTNET35
        Task<string> SendRequestAsync(AddEthereumChainParameter addEthereumChainParameter, object id = null);
#endif
    }


    /// <summary>
    /// The wallet_addEthereumChain RPC method allows Ethereum applications (“dapps”) to suggest chains to be added to the user’s wallet application. The caller must specify a chain ID and some chain metadata. The wallet application may arbitrarily refuse or accept the request. null is returned if the chain was added, and an error otherwise.
    /// https://eips.ethereum.org/EIPS/eip-3085
    /// </summary>
    public class WalletAddEthereumChain : RpcRequestResponseHandler<string>, IWalletAddEthereumChain
    {
        public WalletAddEthereumChain() : this(null)
        {
        }

        public WalletAddEthereumChain(IClient client) : base(client, ApiMethods.wallet_addEthereumChain.ToString())
        {

        }

#if !DOTNET35
        public async Task<string> SendRequestAsync(AddEthereumChainParameter addEthereumChainParameter, object id = null)
        {
            await base.SendRequestAsync(id, addEthereumChainParameter).ConfigureAwait(false);

            return null;
        }
#endif
        public RpcRequest BuildRequest(AddEthereumChainParameter addEthereumChainParameter, object id = null)
        {
            return base.BuildRequest(id, addEthereumChainParameter);
        }
    }
}
