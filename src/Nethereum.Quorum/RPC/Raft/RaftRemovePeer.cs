
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Raft
{

///<Summary>
/// Removes the specified peer from the Raft cluster.
/// 
/// Parameters
/// raftId: string - Raft ID of the peer to be removed from the cluster
/// 
/// Returns
/// result: null    
///</Summary>
    public class RaftRemovePeer : RpcRequestResponseHandler<string>
        {
            public RaftRemovePeer(IClient client) : base(client,ApiMethods.raft_removePeer.ToString()) { }

            public Task<string> SendRequestAsync(string raftId, object id = null)
            {
                return base.SendRequestAsync(id, raftId);
            }
            public RpcRequest BuildRequest(string raftId, object id = null)
            {
                return base.BuildRequest(id, raftId);
            }
        }

    }

