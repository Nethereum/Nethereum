using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth
{
    public interface IEthGetCode
    {
        BlockParameter DefaultBlock { get; set; }

        RpcRequest BuildRequest(string address, BlockParameter block, object id = null);
        Task<string> SendRequestAsync(string address, object id = null);
        Task<string> SendRequestAsync(string address, BlockParameter block, object id = null);
    }
}