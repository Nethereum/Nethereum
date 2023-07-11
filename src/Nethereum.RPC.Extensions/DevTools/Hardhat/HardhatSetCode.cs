
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Modifies the bytecode stored at an account's address    
///</Summary>
    public class HardhatSetCode : RpcRequestResponseHandler<string>
    {
        public HardhatSetCode(IClient client, ApiMethods apiMethod) : base(client, apiMethod.ToString()) { }
        public HardhatSetCode(IClient client) : base(client,ApiMethods.hardhat_setCode.ToString()) { }

        public Task SendRequestAsync(string address, string code, object id = null)
        {
            return base.SendRequestAsync(id, address, code);
        }
        public RpcRequest BuildRequest(string address, string code, object id = null)
        {
            return base.BuildRequest(id, address, code);
        }
    }

}

