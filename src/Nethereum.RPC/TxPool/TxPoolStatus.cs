using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Nethereum.RPC.TxPool.DTOs;

namespace Nethereum.RPC.TxPool
{
    public class TxPoolStatus : GenericRpcRequestResponseHandlerNoParam<TxPoolStatusResponse>, ITxPoolStatus
    {
        public TxPoolStatus(IClient client) : base(client, ApiMethods.txpool_status.ToString())
        {
        }
    }
}
