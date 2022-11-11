using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Raft
{
    /// <summary>
    /// Adds a new node to the network as a learner node. The learner node syncs with the network and can transact, but isn’t part of the Raft cluster and doesn’t provide block confirmation to the minter node.
    /// </summary>
    public interface IRaftAddLearner
    {
        Task<string> SendRequestAsync(string enodeId, object id = null);
        RpcRequest BuildRequest(string enodeId, object id = null);
    }

    /// <summary>
    /// Adds a new node to the network as a learner node. The learner node syncs with the network and can transact, but isn’t part of the Raft cluster and doesn’t provide block confirmation to the minter node.
    /// </summary>
    public class RaftAddLearner : RpcRequestResponseHandler<string>, IRaftAddLearner
    {
        public RaftAddLearner(IClient client) : base(client, ApiMethods.raft_addLearner.ToString())
        {
        }

        public Task<string> SendRequestAsync(string enodeId, object id = null)
        {
            return base.SendRequestAsync(id, enodeId);
        }

        public RpcRequest BuildRequest(string enodeId, object id = null)
        {
            return base.BuildRequest(id, enodeId);
        }
    }

}