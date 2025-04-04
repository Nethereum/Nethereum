using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthSendUserOperationV06
    {
        RpcRequest BuildRequest(UserOperationV06 userOperation, string entryPoint, object id = null);
        Task<string> SendRequestAsync(UserOperationV06 userOperation, string entryPoint, object id = null);
    }
}