
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Sets the given account's code to the specified data. Mines a new block before returning.
/// 
/// Warning: this will result in an invalid state tree.    
///</Summary>
    public class EvmSetAccountCode : RpcRequestResponseHandler<bool>
    {
        public EvmSetAccountCode(IClient client) : base(client,ApiMethods.evm_setAccountCode.ToString()) { }

        public Task<bool> SendRequestAsync(string address, string code, object id = null)
        {
            return base.SendRequestAsync(id, address, code);
        }
        public RpcRequest BuildRequest(string address, string code, object id = null)
        {
            return base.BuildRequest(id, address, code);
        }
    }

}

