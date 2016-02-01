using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth.Transactions
{

    public class EthSendTransaction : RpcRequestResponseHandler<String>
    {
        public EthSendTransaction(RpcClient client) : base(client, ApiMethods.eth_sendTransaction.ToString())
        {

        }

        public async Task<String> SendRequestAsync( EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return await base.SendRequestAsync( id, transactionInput);
        }
        public RpcRequest BuildRequest(EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return base.BuildRequest(id, transactionInput);
        }


    }
}
