using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Admin
{
    public interface IParityPendingTransactionsStats : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {

    }
}