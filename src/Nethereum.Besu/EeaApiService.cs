using Nethereum.JsonRpc.Client;
using Nethereum.Besu.RPC.EEA;
using Nethereum.RPC;

namespace Nethereum.Besu
{
    public class EeaApiService : RpcClientWrapper, IEeaApiService
    {
        public EeaApiService(IClient client) : base(client)
        {
            GetTransactionReceipt = new EeaGetTransactionReceipt(client);
            SendRawTransaction = new EeaSendRawTransaction(client);
        }

        public IEeaGetTransactionReceipt GetTransactionReceipt { get; }
        public IEeaSendRawTransaction SendRawTransaction { get; }
    }
}