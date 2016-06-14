using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Miner
{
    /// <Summary>
    ///     Start the CPU mining process with the given number of threads and generate a new DAG if need be.
    /// </Summary>
    public class MinerStart : RpcRequestResponseHandler<bool>
    {
        public MinerStart(IClient client) : base(client, ApiMethods.miner_start.ToString())
        {
        }

        public Task<bool> SendRequestAsync(int number, object id = null)
        {
            return base.SendRequestAsync(id, number);
        }

        public Task<bool> SendRequestAsync(object id = null)
        {
            return base.SendRequestAsync(id, 1);
        }

        public RpcRequest BuildRequest(int number, object id = null)
        {
            return base.BuildRequest(id, number);
        }
    }
}