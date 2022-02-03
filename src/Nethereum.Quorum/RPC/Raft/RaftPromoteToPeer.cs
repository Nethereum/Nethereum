
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Raft
{
    ///<Summary>
       /// Promotes the specified learner node to peer and thus to be part of the Raft cluster.
/// 
/// Parameters
/// raftId: string - Raft ID of the node to be promoted
/// 
/// Returns
/// result: boolean - indicates if the node is promoted    
    ///</Summary>
    public class RaftPromoteToPeer : RpcRequestResponseHandler<bool>
        {
            public RaftPromoteToPeer(IClient client) : base(client,ApiMethods.raft_promoteToPeer.ToString()) { }

            public Task<bool> SendRequestAsync(string raftId, object id = null)
            {
                return base.SendRequestAsync(id, raftId);
            }
            public RpcRequest BuildRequest(string raftId, object id = null)
            {
                return base.BuildRequest(id, raftId);
            }
        }

    }

