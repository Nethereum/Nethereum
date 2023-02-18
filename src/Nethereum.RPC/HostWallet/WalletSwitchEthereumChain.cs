using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.HostWallet
{
    /// <summary>
    /// The wallet_switchEthereumChain RPC method allows Ethereum applications (“dapps”) to request that the wallet switches its active Ethereum chain, if the wallet has a concept thereof. The caller must specify a chain ID. The wallet application may arbitrarily refuse or accept the request. null is returned if the active chain was switched, and an error otherwise.
    /// https://eips.ethereum.org/EIPS/eip-3326
    /// </summary>
    public interface IWalletSwitchEthereumChain
    {
        RpcRequest BuildRequest(SwitchEthereumChainParameter switchEthereumChainParameter, object id = null);

#if !DOTNET35
        Task<string> SendRequestAsync(SwitchEthereumChainParameter switchEthereumChainParameter, object id = null);
#endif
    }


    /// <summary>
    /// The wallet_switchEthereumChain RPC method allows Ethereum applications (“dapps”) to request that the wallet switches its active Ethereum chain, if the wallet has a concept thereof. The caller must specify a chain ID. The wallet application may arbitrarily refuse or accept the request. null is returned if the active chain was switched, and an error otherwise.
    /// https://eips.ethereum.org/EIPS/eip-3326
    /// </summary>
    public class WalletSwitchEthereumChain : RpcRequestResponseHandler<string>, IWalletSwitchEthereumChain
    {
        public WalletSwitchEthereumChain() : this(null)
        {
        }

        public WalletSwitchEthereumChain(IClient client) : base(client, ApiMethods.wallet_switchEthereumChain.ToString())
        {

        }

#if !DOTNET35
        public async Task<string> SendRequestAsync(SwitchEthereumChainParameter switchEthereumChainParameter, object id = null)
        {
            await base.SendRequestAsync(id, switchEthereumChainParameter);

            return null;
        }
#endif
        public RpcRequest BuildRequest(SwitchEthereumChainParameter switchEthereumChainParameter, object id = null)
        {
            return base.BuildRequest(id, switchEthereumChainParameter);
        }
    }
}
