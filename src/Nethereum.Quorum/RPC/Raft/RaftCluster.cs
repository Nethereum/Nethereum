
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Raft
{

///<Summary>
/// Returns the details of all nodes part of the Raft cluster.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: array - list of node objects with the following fields:
/// 
/// hostName: string - DNS name or the host IP address
/// 
/// nodeActive: boolean - indicates if the node is active in the Raft cluster
/// 
/// nodeId: string - enode ID of the node
/// 
/// p2pPort: number - p2p port
/// 
/// raftId: string - Raft ID of the node
/// 
/// raftPort: number - Raft port
/// 
/// role: string - role of the node in the Raft cluster (minter/verifier/learner); "" if there is no leader at the network level    
    ///</Summary>
    public class RaftCluster : GenericRpcRequestResponseHandlerNoParam<RaftNodeInfo[]>
    {
            public RaftCluster(IClient client) : base(client, ApiMethods.raft_cluster.ToString()) { }
    }

}
            
        