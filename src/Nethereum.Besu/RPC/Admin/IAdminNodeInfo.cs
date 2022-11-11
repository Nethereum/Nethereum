using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.Admin
{
    public interface IAdminNodeInfo : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}