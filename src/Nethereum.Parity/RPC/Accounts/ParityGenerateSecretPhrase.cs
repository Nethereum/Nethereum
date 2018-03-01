using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.Accounts
{
    /// <Summary>
    ///     parity_generateSecretPhrase
    ///     Creates a secret phrase that can be associated with an account.
    ///     Parameters
    ///     None
    ///     Returns
    ///     String - The secret phrase.
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_generateSecretPhrase","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type:
    ///     application/json" -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": "boasting breeches reshape reputably exit handrail stony jargon moneywise unhinge handed ruby"
    ///     }
    /// </Summary>
    public class ParityGenerateSecretPhrase : GenericRpcRequestResponseHandlerNoParam<string>
    {
        public ParityGenerateSecretPhrase(IClient client) : base(client,
            ApiMethods.parity_generateSecretPhrase.ToString())
        {
        }
    }
}