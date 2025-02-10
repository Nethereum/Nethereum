using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthSendUserOperation
    {
        RpcRequest BuildRequest(UserOperationV06 userOperation, string entryPoint, object id = null);
        RpcRequest BuildRequest(UserOperation userOperation, string entryPoint, object id = null);
        Task<string> SendRequestAsync(UserOperationV06 userOperation, string entryPoint, object id = null);
        Task<string> SendRequestAsync(UserOperation userOperation, string entryPoint, object id = null);
    }
}