
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Stops Hardhat Network from impersonating the given address    
///</Summary>
    public class HardhatStopImpersonatingAccount : RpcRequestResponseHandler<string>
    {
        public HardhatStopImpersonatingAccount(IClient client) : base(client,ApiMethods.hardhat_stopImpersonatingAccount.ToString()) { }

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

