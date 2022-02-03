using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC.Raft
{
    /// <summary>
    /// Returns the role of the current node in the Raft cluster
    /// result: string - role of the node in the Raft cluster (minter/verifier/learner); "" if there is no leader at the network level
    /// </summary>
    public class RaftRole : GenericRpcRequestResponseHandlerNoParam<string>, IRaftRole
    {
        public RaftRole(IClient client) : base(client, ApiMethods.raft_role.ToString())
        {
        }
    }


}