
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Removes the given transaction from the mempool, if it exists    
///</Summary>
    public class HardhatDropTransaction : RpcRequestResponseHandler<bool>
    {

        public HardhatDropTransaction(IClient client, ApiMethods apiMethod) : base(client, apiMethod.ToString()) { }
        public HardhatDropTransaction(IClient client) : base(client, ApiMethods.hardhat_dropTransaction.ToString()) { }

        public Task<bool> SendRequestAsync(string txnHash, object id = null)
        {
            return base.SendRequestAsync(id, txnHash);
        }
        public RpcRequest BuildRequest(string txnHash, object id = null)
        {
            return base.BuildRequest(id, txnHash);
        }
    }

}

