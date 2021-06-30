using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.RPC.EEA.DTOs;

namespace Nethereum.Besu.RPC.EEA
{
    public interface IEeaGetTransactionReceipt
    {
        Task<EeaTransactionReceipt> SendRequestAsync(string transactionHash, object id = null);
        RpcRequest BuildRequest(string transactionHash, object id = null);
    }
}