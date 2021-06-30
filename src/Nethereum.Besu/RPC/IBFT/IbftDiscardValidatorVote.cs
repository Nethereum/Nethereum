using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.IBFT
{
    /// <Summary>
    ///     Discards a proposal to add or remove a validator with the specified address.
    /// </Summary>
    public class IbftDiscardValidatorVote : RpcRequestResponseHandler<bool>, IIbftDiscardValidatorVote
    {
        public IbftDiscardValidatorVote(IClient client) : base(client, ApiMethods.ibft_discardValidatorVote.ToString())
        {
        }

        public async Task<bool> SendRequestAsync(string validatorAddress, object id = null)
        {
            return await base.SendRequestAsync(id, validatorAddress);
        }

        public RpcRequest BuildRequest(string validatorAddress, object id = null)
        {
            return base.BuildRequest(id, validatorAddress);
        }
    }
}