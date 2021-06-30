using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.Permissioning
{
    /// <Summary>
    ///     Removes accounts (participants) from the accounts whitelist.
    /// </Summary>
    public class PermRemoveAccountsFromWhitelist : RpcRequestResponseHandler<string>, IPermRemoveAccountsFromWhitelist
    {
        public PermRemoveAccountsFromWhitelist(IClient client) : base(client,
            ApiMethods.perm_removeAccountsFromWhitelist.ToString())
        {
        }

        public async Task<string> SendRequestAsync(string[] addresses, object id = null)
        {
            return await base.SendRequestAsync(id, new object[] { addresses });
        }

        public RpcRequest BuildRequest(string[] addresses, object id = null)
        {
            return base.BuildRequest(id, new object[] { addresses });
        }
    }
}