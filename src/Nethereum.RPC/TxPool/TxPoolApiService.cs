using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.TxPool
{
    public class TxPoolApiService : ITxPoolApiService
    {
        public TxPoolApiService(IClient client)
        {
            Content = new TxPoolContent(client);
            ContentFrom = new TxPoolContentFrom(client);
            Status = new TxPoolStatus(client);
        }

        public ITxPoolContent Content { get; private set; }

        public ITxPoolContentFrom ContentFrom { get; private set; }

        public ITxPoolStatus Status { get; private set; }
    }
}
