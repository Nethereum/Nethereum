using Nethereum.Geth.RPC.TxnPool;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Geth
{
    public class TxnPoolApiService : RpcClientWrapper, ITxnPoolApiService
    {
        public TxnPoolApiService(IClient client) : base(client)
        {
            PoolContent = new TxnPoolContent(client);
            PoolInspect = new TxnPoolInspect(client);
            PoolStatus = new TxnPoolStatus(client);
        }

        public ITxnPoolContent PoolContent { get; }
        public ITxnPoolInspect PoolInspect { get; }
        public ITxnPoolStatus PoolStatus { get; }
    }
}