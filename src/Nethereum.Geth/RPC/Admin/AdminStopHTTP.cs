using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Geth.RPC.Admin
{
    /// <Summary>
    ///     The stopHTTP administrative method closes the currently open HTTP RPC endpoint. As the node can only have a single
    ///     HTTP endpoint running, this method takes no parameters, returning a boolean whether the endpoint was closed or not.
    /// </Summary>
    public class AdminStopHTTP : GenericRpcRequestResponseHandlerNoParam<bool>, IAdminStopHTTP
    {
        public AdminStopHTTP(IClient client) : base(client, ApiMethods.admin_stopHTTP.ToString())
        {
        }
    }
}