using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.DebugNode
{
    public class DebugGetRawTransaction : RpcRequestResponseHandler<string>, IDebugGetRawTransaction
    {
        public DebugGetRawTransaction(IClient client)
            : base(client, ApiMethods.debug_getRawTransaction.ToString())
        {
        }

        public Task<string> SendRequestAsync(string transactionHash, object id = null)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));
            return base.SendRequestAsync(id, transactionHash);
        }


        public RpcRequest BuildRequest(string transactionHash, object id = null)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));
            return base.BuildRequest(id, transactionHash);
        }
    }
}