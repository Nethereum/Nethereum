
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Checks if the specified node is allowed to join the network.
    /// 
    /// Parameters
    /// enodeId: string - enode ID
    /// 
    /// ipAddress: string - IP address of the node
    /// 
    /// portNum: number - port number
    /// 
    /// Returns
    /// result: boolean - indicates if the connection is allowed or not    
    ///</Summary>
    public interface IQuorumPermissionConnectionAllowed
    {
        Task<bool> SendRequestAsync(string enodeId, string ipAddress, int portNumber, object id = null);
        RpcRequest BuildRequest(string enodeId, string ipAddress, int portNumber, object id = null);
    }

    ///<Summary>
/// Checks if the specified node is allowed to join the network.
/// 
/// Parameters
/// enodeId: string - enode ID
/// 
/// ipAddress: string - IP address of the node
/// 
/// portNum: number - port number
/// 
/// Returns
/// result: boolean - indicates if the connection is allowed or not    
///</Summary>
    public class QuorumPermissionConnectionAllowed : RpcRequestResponseHandler<bool>, IQuorumPermissionConnectionAllowed
    {
        public QuorumPermissionConnectionAllowed(IClient client) : base(client,ApiMethods.quorumPermission_connectionAllowed.ToString()) { }

        public Task<bool> SendRequestAsync(string enodeId, string ipAddress, int portNumber, object id = null)
        {
            return base.SendRequestAsync(id, enodeId, ipAddress, portNumber);
        }
        public RpcRequest BuildRequest(string enodeId, string ipAddress, int portNumber, object id = null)
        {
            return base.BuildRequest(id, enodeId, ipAddress, portNumber);
        }
    }

}

