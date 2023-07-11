
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Sets the given account's storage slot to the specified data. Mines a new block before returning.
/// 
/// Warning: this will result in an invalid state tree.    
///</Summary>
    public class EvmSetAccountStorageAt : RpcRequestResponseHandler<bool>
    {
        public EvmSetAccountStorageAt(IClient client) : base(client,ApiMethods.evm_setAccountStorageAt.ToString()) { }

        public Task<bool> SendRequestAsync(string address, HexBigInteger slot, string hexData, object id = null)
        {
            return base.SendRequestAsync(id, address, slot, hexData);
        }
        public RpcRequest BuildRequest(string address, HexBigInteger slot, string hexData, object id = null)
        {
            return base.BuildRequest(id, address, slot, hexData);
        }
    }

}

