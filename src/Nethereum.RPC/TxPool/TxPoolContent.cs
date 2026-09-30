using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Nethereum.RPC.TxPool.DTOs;

namespace Nethereum.RPC.TxPool
{
    public class TxPoolContent : GenericRpcRequestResponseHandlerNoParam<TxPoolContentResponse>, ITxPoolContent
    {
        public TxPoolContent(IClient client) : base(client, ApiMethods.txpool_content.ToString())
        {
        }
    }
}
