using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.IBFT
{
    /// <Summary>
    ///     Returns current votes.
    ///     result: object - Map of account addresses to corresponding boolean values indicating the vote for each account.
    ///     If the boolean value is true, the vote is to add a validator. If false, the proposal is to remove a validator.
    /// </Summary>
    public class IbftGetPendingVotes : GenericRpcRequestResponseHandlerNoParam<JObject>, IIbftGetPendingVotes
    {
        public IbftGetPendingVotes(IClient client) : base(client, ApiMethods.ibft_getPendingVotes.ToString())
        {
        }
    }
}