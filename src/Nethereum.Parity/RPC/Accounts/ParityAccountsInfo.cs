using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Accounts
{
    /// <Summary>
    ///     parity_accountsInfo
    ///     Provides metadata for accounts.
    ///     Parameters
    ///     None
    ///     Returns
    ///     Object - Maps account address to metadata.
    ///     name: String - Account name
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_accountsInfo","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type:
    ///     application/json" -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": {
    ///     "0x0024d0c7ab4c52f723f3aaf0872b9ea4406846a4": {
    ///     "name": "Foo"
    ///     },
    ///     "0x004385d8be6140e6f889833f68b51e17b6eacb29": {
    ///     "name": "Bar"
    ///     },
    ///     "0x009047ed78fa2be48b62aaf095b64094c934dab0": {
    ///     "name": "Baz"
    ///     }
    ///     }
    ///     }
    /// </Summary>
    public class ParityAccountsInfo : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public ParityAccountsInfo(IClient client) : base(client, ApiMethods.parity_accountsInfo.ToString())
        {
        }
    }
}