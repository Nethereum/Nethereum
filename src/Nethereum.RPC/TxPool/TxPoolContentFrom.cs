using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.TxPool.DTOs;

namespace Nethereum.RPC.TxPool
{
    public class TxPoolContentFrom : RpcRequestResponseHandler<TxPoolContentFromResponse>, ITxPoolContentFrom
    {
        public TxPoolContentFrom(IClient client) : base(client, ApiMethods.txpool_contentFrom.ToString())
        {
        }

        public Task<TxPoolContentFromResponse> SendRequestAsync(string address, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return base.SendRequestAsync(id, address);
        }

        public RpcRequest BuildRequest(string address, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return base.BuildRequest(id, address);
        }
    }
}
