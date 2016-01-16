using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using RPCRequestResponseHandlers;

namespace Ethereum.RPC.Eth
{

    public class EthSendTransaction : RpcRequestResponseHandler<String>
    {
        public EthSendTransaction() : base(ApiMethods.eth_sendTransaction.ToString())
        {

        }

        public async Task<String> SendRequestAsync(RpcClient client, EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return await base.SendRequestAsync(client, id, transactionInput);
        }
        public RpcRequest BuildRequest(EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return base.BuildRequest(id, transactionInput);
        }


    }
}
