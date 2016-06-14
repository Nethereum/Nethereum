using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Miner
{
    /// <Summary>
    ///     Sets the minimal accepted gas price when mining transactions. Any transactions that are below this limit are
    ///     excluded from the mining process.
    /// </Summary>
    public class MinerSetGasPrice : RpcRequestResponseHandler<bool>
    {
        public MinerSetGasPrice(IClient client) : base(client, ApiMethods.miner_setGasPrice.ToString())
        {
        }

        public Task<bool> SendRequestAsync(HexBigInteger price, object id = null)
        {
            return base.SendRequestAsync(id, price);
        }

        public RpcRequest BuildRequest(HexBigInteger price, object id = null)
        {
            return base.BuildRequest(id, price);
        }
    }
}