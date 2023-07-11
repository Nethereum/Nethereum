
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Adds any arbitrary account to the personal namespace.
/// 
/// Note: accounts already known to the personal namespace and accounts returned by eth_accounts cannot be re-added using this method.    
///</Summary>
    public class EvmAddAccount : RpcRequestResponseHandler<bool>
    {
        public EvmAddAccount(IClient client) : base(client,ApiMethods.evm_addAccount.ToString()) { }

        public Task<bool> SendRequestAsync(string address, string passPhrase, object id = null)
        {
            return base.SendRequestAsync(id, address, passPhrase);
        }
        public RpcRequest BuildRequest(string address, string passPhrase, object id = null)
        {
            return base.BuildRequest(id, address, passPhrase);
        }
    }

}

