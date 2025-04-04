using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthEstimateUserOperationGasV06
    {
        RpcRequest BuildRequest(UserOperationV06 userOperation, string entryPoint, object id = null);
        RpcRequest BuildRequest(UserOperationV06 userOperation, string entryPoint, StateChange stateChange, object id = null);
        Task<UserOperationGasEstimate> SendRequestAsync(UserOperationV06 userOperation, string entryPoint, object id = null);
        Task<UserOperationGasEstimate> SendRequestAsync(UserOperationV06 userOperation, string entryPoint, StateChange stateChange, object id = null);
    }
}