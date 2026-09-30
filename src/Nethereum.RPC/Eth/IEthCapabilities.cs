using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth
{
    public interface IEthCapabilities
    {
        Task<EthCapabilitiesResult> SendRequestAsync(object id = null);
        RpcRequest BuildRequest(object id = null);
    }
}
