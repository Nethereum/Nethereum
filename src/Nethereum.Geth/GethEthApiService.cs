using Nethereum.Geth.RPC.GethEth;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Geth
{
    public class GethEthApiService : RpcClientWrapper, IGethEthApiService
    {
        public GethEthApiService(IClient client) : base(client)
        {
            PendingTransactions = new EthPendingTransactions(client);
        }

        public IEthPendingTransactions PendingTransactions { get; }
    }
}