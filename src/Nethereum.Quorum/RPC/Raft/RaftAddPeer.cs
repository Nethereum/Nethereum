
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Raft
{

///<Summary>
/// Adds a new peer to the network.
/// 
/// Parameters
/// enodeId: string - enode ID of the node to be added to the network
/// 
/// Returns
/// result: string - Raft ID for the node being added, or an error message if the node is already part of the network    
    ///</Summary>
    public class RaftAddPeer : RpcRequestResponseHandler<string>
        {
            public RaftAddPeer(IClient client) : base(client,ApiMethods.raft_addPeer.ToString()) { }

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

