using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthGetUserOperationByHashV06
    {
        RpcRequest BuildRequest(string userOpHash, object id = null);
        Task<UserOperationV06> SendRequestAsync(string userOpHash, object id = null);
    }
}