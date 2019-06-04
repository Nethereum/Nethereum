using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Txpool
{
    public interface ITxpoolPantheonStatistics : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}