using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Personal
{
    /// <Summary>
    ///     Removes the private key with given address from memory. The account can no longer be used to send transactions.
    /// </Summary>
    public class PersonalLockAccount : RpcRequestResponseHandler<bool>
    {
        public PersonalLockAccount(IClient client) : base(client, ApiMethods.personal_lockAccount.ToString())
        {
        }

        public Task<bool> SendRequestAsync(string account, object id = null)
        {
            return base.SendRequestAsync(id, account);
        }

        public RpcRequest BuildRequest(string account, object id = null)
        {
            return base.BuildRequest(id, account);
        }
    }
}