using Nethereum.RPC.Infrastructure;

namespace Nethereum.Quorum.RPC.Raft
{
    /// <summary>
    /// Returns the role of the current node in the Raft cluster
    /// result: string - role of the node in the Raft cluster (minter/verifier/learner); "" if there is no leader at the network level
    /// </summary>
    public interface IRaftRole : IGenericRpcRequestResponseHandlerNoParam<string>
    {

    }
}