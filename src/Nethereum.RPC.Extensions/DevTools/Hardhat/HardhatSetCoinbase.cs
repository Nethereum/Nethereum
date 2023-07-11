
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Sets the coinbase address to be used in new blocks    
///</Summary>
    public class HardhatSetCoinbase : RpcRequestResponseHandler<string>
    {
        public HardhatSetCoinbase(IClient client, ApiMethods apiMethod) : base(client, apiMethod.ToString()) { }
        public HardhatSetCoinbase(IClient client) : base(client,ApiMethods.hardhat_setCoinbase.ToString()) { }

        public Task SendRequestAsync(string address, object id = null)
        {
            return base.SendRequestAsync(id, address);
        }
        public RpcRequest BuildRequest(string address, object id = null)
        {
            return base.BuildRequest(id, address);
        }
    }

}

