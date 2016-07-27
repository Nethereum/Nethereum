using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public class EthSendTransaction : RpcRequestResponseHandler<string>
    {
        public EthSendTransaction(IClient client) : base(client, ApiMethods.eth_sendTransaction.ToString())
        {
        }

        public Task<string> SendRequestAsync(TransactionInput input, object id = null)
        {
            return base.SendRequestAsync(id, input);
        }

        public RpcRequest BuildRequest(TransactionInput input, object id = null)
        {
            return base.BuildRequest(id, input);
        }
    }
}