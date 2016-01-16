using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Ethereum.RPC.Generic;
using RPCRequestResponseHandlers;

namespace Ethereum.RPC.Eth
{
    public class EthCall : RpcRequestResponseHandler<string>
    {
        public EthCall() : base(ApiMethods.eth_call.ToString())
        {

        }

        public async Task<string> SendRequestAsync(RpcClient client, EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return await base.SendRequestAsync(client, id, transactionInput, BlockParameter.CreateLatest());
        }
        public RpcRequest BuildRequest(EthSendTransactionInput transactionInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return base.BuildRequest(id, transactionInput, BlockParameter.CreateLatest());
        }


    }
}