using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Miner
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

        public RpcRequest BuildRequest(HexBigInteger price, object id = null)
        {
            if (price == null) throw new ArgumentNullException(nameof(price));
            return base.BuildRequest(id, price);
        }

        public Task<bool> SendRequestAsync(HexBigInteger price, object id = null)
        {
            if (price == null) throw new ArgumentNullException(nameof(price));
            return base.SendRequestAsync(id, price);
        }
    }
}