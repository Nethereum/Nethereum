using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.Accounts
{
    /// <Summary>
    ///     parity_defaultAccount
    ///     Returns the defaultAccount that is to be used with transactions
    ///     Parameters
    ///     None
    ///     Returns
    ///     Address - The account address
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_defaultAccount","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type:
    ///     application/json" -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x63Cf90D3f0410092FC0fca41846f596223979195"
    ///     }
    /// </Summary>
    public class ParityDefaultAccount : GenericRpcRequestResponseHandlerNoParam<string>, IParityDefaultAccount
    {
        public ParityDefaultAccount(IClient client) : base(client, ApiMethods.parity_defaultAccount.ToString())
        {
        }
    }
}