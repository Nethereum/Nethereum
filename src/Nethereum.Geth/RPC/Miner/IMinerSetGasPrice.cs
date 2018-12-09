using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Miner
{
    public interface IMinerSetGasPrice
    {
        RpcRequest BuildRequest(HexBigInteger price, object id = null);
        Task<bool> SendRequestAsync(HexBigInteger price, object id = null);
    }
}