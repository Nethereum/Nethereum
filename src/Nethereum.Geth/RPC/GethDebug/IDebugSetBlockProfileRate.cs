using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugSetBlockProfileRate
    {
        RpcRequest BuildRequest(long rate, object id = null);
        Task<object> SendRequestAsync(long rate, object id = null);
    }
}