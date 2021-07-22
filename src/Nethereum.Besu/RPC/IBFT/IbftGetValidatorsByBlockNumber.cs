using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Besu.RPC.IBFT
{
    /// <Summary>
    ///     Lists the validators defined in the specified block.
    /// </Summary>
    public class IbftGetValidatorsByBlockNumber : RpcRequestResponseHandler<string[]>, IIbftGetValidatorsByBlockNumber
    {
        public IbftGetValidatorsByBlockNumber(IClient client) : base(client,
            ApiMethods.ibft_getValidatorsByBlockNumber.ToString())
        {
        }

        public Task<string[]> SendRequestAsync(BlockParameter block, object id = null)
        {
            return base.SendRequestAsync(id, block);
        }

        public RpcRequest BuildRequest(BlockParameter block, object id = null)
        {
            return base.BuildRequest(id, block);
        }
    }
}