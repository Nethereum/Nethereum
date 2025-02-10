using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthGetUserOperationByHash
    {
        RpcRequest BuildRequest(string userOpHash, object id = null);
        Task<UserOperation> SendRequestAsync(string userOpHash, object id = null);
    }
}