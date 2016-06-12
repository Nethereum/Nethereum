using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
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
    public class PersonalNewAccount : RpcRequestResponseHandler<string>
    {
        public PersonalNewAccount(IClient client) : base(client, ApiMethods.personal_newAccount.ToString())
        {
        }

        public async Task<string> SendRequestAsync(string passPhrase, object id = null)
        {
            return await base.SendRequestAsync(id, passPhrase).ConfigureAwait(false);
        }

        public RpcRequest BuildRequest(string passPhrase, object id = null)
        {
            return base.BuildRequest(id, passPhrase);
        }
    }
}