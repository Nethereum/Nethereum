using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.Permissioning
{
    /// <Summary>
    ///     Adds accounts (participants) to the accounts whitelist.
    ///     Returns
    ///     result - Success or error. Errors include attempting to add accounts already on the whitelist or including invalid
    ///     account addresses.
    /// </Summary>
    public class PermAddAccountsToWhitelist : RpcRequestResponseHandler<string>, IPermAddAccountsToWhitelist
    {
        public PermAddAccountsToWhitelist(IClient client) : base(client,
            ApiMethods.perm_addAccountsToWhitelist.ToString())
        {
        }

        public Task<string> SendRequestAsync(string[] addresses, object id = null)
        {
            return base.SendRequestAsync(id, new object[] {addresses});
        }

        public RpcRequest BuildRequest(string[] addresses, object id = null)
        {
            return base.BuildRequest(id, new object[] {addresses});
        }
    }
}