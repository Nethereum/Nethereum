using System;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexConvertors.Extensions;
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
            if (account == null) throw new ArgumentNullException(nameof(account));
            return base.SendRequestAsync(id, account.EnsureHexPrefix());
        }

        public RpcRequest BuildRequest(string account, object id = null)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            return base.BuildRequest(id, account.EnsureHexPrefix());
        }
    }
}