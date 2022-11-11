using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Filters
{
    public interface IEthUninstallFilter
    {
        RpcRequest BuildRequest(HexBigInteger filterId, object id = null);
        Task<bool> SendRequestAsync(HexBigInteger filterId, object id = null);
    }
}