using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public class EthSendTransaction : RpcRequestResponseHandler<string>
    {
        public EthSendTransaction(IClient client) : base(client, ApiMethods.eth_sendTransaction.ToString())
        {
        }

        public async Task<string> SendRequestAsync(TransactionInput input, object id = null)
        {
            return await base.SendRequestAsync(id, input);
        }

        public RpcRequest BuildRequest(TransactionInput input, object id = null)
        {
            return base.BuildRequest(id, input);
        }
    }
}