
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Raft
{

///<Summary>
/// Returns the enode ID of the leader node.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: string - enode ID of the leader, or an error message if there is no leader
/// raftPort: number - Raft port
/// 
/// role: string - role of the node in the Raft cluster (minter/verifier/learner); "" if there is no leader at the network level    
    ///</Summary>
    public class RaftLeader : GenericRpcRequestResponseHandlerNoParam<string>
    {
         public RaftLeader(IClient client) : base(client, ApiMethods.raft_leader.ToString()) { }
    }

}
            
        