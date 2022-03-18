using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    /// <Summary>
    ///     The addTrustedPeer administrative method requests adding a new remote node to the list of tracked static nodes. The node
    ///     will try to maintain connectivity to these nodes at all times, reconnecting every once in a while if the remote
    ///     connection goes down.
    ///     This method allows a remote node to always connect, even if slots are full
    /// </Summary>
    public class AdminAddTrustedPeer : RpcRequestResponseHandler<bool>, IAdminAddTrustedPeer
    {
        public AdminAddTrustedPeer(IClient client) : base(client, ApiMethods.admin_addTrustedPeer.ToString())
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