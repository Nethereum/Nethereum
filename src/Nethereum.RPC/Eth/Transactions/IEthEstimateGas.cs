using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthEstimateGas
    {
        RpcRequest BuildRequest(CallInput callInput, object id = null);
        Task<HexBigInteger> SendRequestAsync(CallInput callInput, object id = null);
    }
}