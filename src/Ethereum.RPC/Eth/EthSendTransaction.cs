using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using RPCRequestResponseHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ethereum.RPC.SendTransaction
{

    public class EthSendTransaction : RpcRequestResponseHandler<String>
    {
        public EthSendTransaction() : base(ApiMethods.eth_sendTransaction.ToString())
        {

        }

        public async Task<String> SendRequestAsync(RpcClient client, SendTransaction.EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return await base.SendRequestAsync(client, id, transactionInput);
        }
        public RpcRequest BuildRequest(SendTransaction.EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return base.BuildRequest(id, transactionInput);
        }


    }
}
