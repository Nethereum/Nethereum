using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Admin
{
    public interface IAdminPeers : IGenericRpcRequestResponseHandlerNoParam<JArray>
    {
    }
}