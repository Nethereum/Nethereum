using System;
using System.Threading.Tasks;
 
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Personal
{
    /// <Summary>
    ///     Create a new account
    ///     Parameters
    ///     string, passphrase to protect the account
    ///     Return
    ///     string address of the new account
    ///     Example
    ///     personal.newAccount("mypasswd")
    /// </Summary>
    public class PersonalNewAccount : RpcRequestResponseHandler<string>, IPersonalNewAccount
    {
        public PersonalNewAccount(IClient client) : base(client, ApiMethods.personal_newAccount.ToString())
        {
        }

        public Task<string> SendRequestAsync(string passPhrase, object id = null)
        {
            if (passPhrase == null) throw new ArgumentNullException(nameof(passPhrase));
            return SendRequestAsync(id, passPhrase);
        }

        public RpcRequest BuildRequest(string passPhrase, object id = null)
        {
            if (passPhrase == null) throw new ArgumentNullException(nameof(passPhrase));
            return base.BuildRequest(id, passPhrase);
        }
    }
}