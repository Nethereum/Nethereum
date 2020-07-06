using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.TxnPool
{
    public interface ITxnPoolContent: IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}