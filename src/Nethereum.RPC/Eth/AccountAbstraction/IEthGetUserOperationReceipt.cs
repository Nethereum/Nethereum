using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthGetUserOperationReceipt
    {
        RpcRequest BuildRequest(string userOpHash, object id = null);
        Task<UserOperationReceipt> SendRequestAsync(string userOpHash, object id = null);
    }
}