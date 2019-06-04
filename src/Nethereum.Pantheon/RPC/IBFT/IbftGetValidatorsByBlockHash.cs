using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Pantheon.RPC.IBFT
{
    /// <Summary>
    ///     Lists the validators defined in the specified block.
    /// </Summary>
    public class IbftGetValidatorsByBlockHash : RpcRequestResponseHandler<string[]>, IIbftGetValidatorsByBlockHash
    {
        public IbftGetValidatorsByBlockHash(IClient client) : base(client,
            ApiMethods.ibft_getValidatorsByBlockHash.ToString())
        {
        }

        public async Task<string[]> SendRequestAsync(string blockHash, object id = null)
        {
            return await base.SendRequestAsync(id, blockHash);
        }

        public RpcRequest BuildRequest(string blockHash, object id = null)
        {
            return base.BuildRequest(id, blockHash);
        }
    }
}