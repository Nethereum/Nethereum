using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthEstimateUserOperationGas
    {
        RpcRequest BuildRequest(UserOperation userOperation, string entryPoint, object id = null);
        RpcRequest BuildRequest(UserOperation userOperation, string entryPoint, StateChange stateChange, object id = null);
        Task<UserOperationGasEstimate> SendRequestAsync(UserOperation userOperation, string entryPoint, object id = null);
        Task<UserOperationGasEstimate> SendRequestAsync(UserOperation userOperation, string entryPoint, StateChange stateChange, object id = null);
    }
}