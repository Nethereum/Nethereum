
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Removes an account from the personal namespace.
/// 
/// Note: accounts not known to the personal namespace cannot be removed using this method.    
///</Summary>
    public class EvmRemoveAccount : RpcRequestResponseHandler<bool>
    {
        public EvmRemoveAccount(IClient client) : base(client,ApiMethods.evm_removeAccount.ToString()) { }

        public Task<bool> SendRequestAsync(string address, string passphrase, object id = null)
        {
            return base.SendRequestAsync(id, address, passphrase);
        }
        public RpcRequest BuildRequest(string address, string passphrase, object id = null)
        {
            return base.BuildRequest(id, address, passphrase);
        }
    }

}

