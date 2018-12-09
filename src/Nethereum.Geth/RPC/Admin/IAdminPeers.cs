using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Admin
{
    public interface IAdminPeers : IGenericRpcRequestResponseHandlerNoParam<JArray>
    {

    }
}