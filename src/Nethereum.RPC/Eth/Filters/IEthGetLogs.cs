using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Filters
{
    public interface IEthGetLogs
    {
        RpcRequest BuildRequest(NewFilterInput newFilter, object id = null);
        Task<FilterLog[]> SendRequestAsync(NewFilterInput newFilter, object id = null);
    }
}