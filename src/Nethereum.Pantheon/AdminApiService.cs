using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.RPC.Admin;
using Nethereum.RPC;


namespace Nethereum.Pantheon
{
    public class AdminApiService : RpcClientWrapper, IAdminApiService
    {
        public AdminApiService(IClient client) : base(client)
        {
            AddPeer = new AdminAddPeer(client);
            NodeInfo = new AdminNodeInfo(client);
            Peers = new AdminPeers(client);
            RemovePeer = new AdminRemovePeer(client);
        }

        public IAdminAddPeer AddPeer { get; }
        public IAdminNodeInfo NodeInfo { get; }
        public IAdminPeers Peers { get; }
        public IAdminRemovePeer RemovePeer { get; }
    }
}