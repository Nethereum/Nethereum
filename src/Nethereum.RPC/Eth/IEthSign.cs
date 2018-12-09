using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth
{
    public interface IEthSign
    {
        RpcRequest BuildRequest(string address, string data, object id = null);
        Task<string> SendRequestAsync(string address, string data, object id = null);
    }
}