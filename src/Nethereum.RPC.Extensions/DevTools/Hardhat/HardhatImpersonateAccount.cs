
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Allows Hardhat Network to sign transactions as the given address    
///</Summary>
    public class HardhatImpersonateAccount : RpcRequestResponseHandler<string>
    {
        public HardhatImpersonateAccount(IClient client, ApiMethods apiMethod) : base(client, apiMethod.ToString()) { }
        public HardhatImpersonateAccount(IClient client) : base(client,ApiMethods.hardhat_impersonateAccount.ToString()) { }

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

