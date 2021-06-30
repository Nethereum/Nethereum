using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.Txpool
{
    public interface ITxpoolBesuTransactions : IGenericRpcRequestResponseHandlerNoParam<JArray>
    {
    }
}