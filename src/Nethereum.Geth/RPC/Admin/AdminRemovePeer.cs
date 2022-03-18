using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    /// <Summary>
    ///     The removePeer administrative method requests remove a new node to the list of tracked static nodes. 
    ///     The method accepts a single argument, the enode URL of the remote peer to remove and returns a BOOL
    ///     indicating whether the peer was accepted for tracking or some error occurred.
    /// </Summary>
    public class AdminRemovePeer : RpcRequestResponseHandler<bool>, IAdminRemovePeer
    {
        public AdminRemovePeer(IClient client) : base(client, ApiMethods.admin_removePeer.ToString())
        {
        }

        public RpcRequest BuildRequest(string enodeUrl, object id = null)
        {
            if (enodeUrl == null) throw new ArgumentNullException(nameof(enodeUrl));
            return base.BuildRequest(id, enodeUrl);
        }

        public Task<bool> SendRequestAsync(string enodeUrl, object id = null)
        {
            if (enodeUrl == null) throw new ArgumentNullException(nameof(enodeUrl));
            return base.SendRequestAsync(id, enodeUrl);
        }
    }
}