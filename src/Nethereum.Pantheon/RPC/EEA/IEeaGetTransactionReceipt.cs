using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.RPC.EEA.DTOs;

namespace Nethereum.Pantheon.RPC.EEA
{
    public interface IEeaGetTransactionReceipt
    {
        Task<EeaTransactionReceipt> SendRequestAsync(string transactionHash, object id = null);
        RpcRequest BuildRequest(string transactionHash, object id = null);
    }
}