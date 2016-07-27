using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Admin
{
    /// <Summary>
    ///     The addPeer administrative method requests adding a new remote node to the list of tracked static nodes. The node
    ///     will try to maintain connectivity to these nodes at all times, reconnecting every once in a while if the remote
    ///     connection goes down.
    ///     The method accepts a single argument, the enode URL of the remote peer to start tracking and returns a BOOL
    ///     indicating whether the peer was accepted for tracking or some error occurred.
    /// </Summary>
    public class AdminAddPeer : RpcRequestResponseHandler<bool>
    {
        public AdminAddPeer(IClient client) : base(client, ApiMethods.admin_addPeer.ToString())
        {
        }

        public Task<bool> SendRequestAsync(string enodeUrl, object id = null)
        {
            return base.SendRequestAsync(id, enodeUrl);
        }

        public RpcRequest BuildRequest(string enodeUrl, object id = null)
        {
            return base.BuildRequest(id, enodeUrl);
        }
    }
}