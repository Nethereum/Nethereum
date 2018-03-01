using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.Admin
{
    /// <Summary>
    ///     parity_listVaults
    ///     Returns a list of all available vaults
    ///     Parameters
    ///     None
    ///     Returns
    ///     Array - Names of all available vaults
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_listVaults","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json"
    ///     -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": "['Personal','Work']"
    ///     }
    /// </Summary>
    public class ParityListVaults : GenericRpcRequestResponseHandlerNoParam<string[]>
    {
        public ParityListVaults(IClient client) : base(client, ApiMethods.parity_listVaults.ToString())
        {
        }
    }
}