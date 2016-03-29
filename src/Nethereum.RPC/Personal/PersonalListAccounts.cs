using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Personal
{
    /// <Summary>
    ///     personal_listAccounts
    ///     List all accounts
    ///     Parameters
    ///     none
    ///     Return
    ///     array collection with accounts
    ///     Example
    ///     personal.listAccounts
    /// </Summary>
    public class PersonalListAccounts : GenericRpcRequestResponseHandlerNoParam<string[]>
    {
        public PersonalListAccounts(IClient client) : base(client, ApiMethods.personal_listAccounts.ToString())
        {
        }
    }
}