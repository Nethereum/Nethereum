using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.DebugGeth
{
    /// <Summary>
    ///     Fetches and retrieves the seed hash of the block by number
    /// </Summary>
    public class DebugSeedHash : RpcRequestResponseHandler<string>
    {
        public DebugSeedHash(IClient client) : base(client, ApiMethods.debug_seedHash.ToString())
        {
        }

        public Task<string> SendRequestAsync(ulong blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, blockNumber);
        }

        public RpcRequest BuildRequest(ulong blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber);
        }
    }
}