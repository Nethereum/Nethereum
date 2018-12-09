using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthSendRawTransaction
    {
        RpcRequest BuildRequest(string signedTransactionData, object id = null);
        Task<string> SendRequestAsync(string signedTransactionData, object id = null);
    }
}