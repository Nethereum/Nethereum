using Nethereum.Geth.RPC.Admin;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Geth
{
    public class AdminApiService : RpcClientWrapper, IAdminApiService
    {
        public AdminApiService(IClient client) : base(client)
        {
            AddPeer = new AdminAddPeer(client);
            Datadir = new AdminDatadir(client);
            NodeInfo = new AdminNodeInfo(client);
            SetSolc = new AdminSetSolc(client);
            StartRPC = new AdminStartRPC(client);
            StartWS = new AdminStartWS(client);
            StopRPC = new AdminStopRPC(client);
            StopWS = new AdminStopWS(client);
            Peers = new AdminPeers(client);
        }

        public IAdminAddPeer AddPeer { get; }
        public IAdminDatadir Datadir { get; }
        public IAdminNodeInfo NodeInfo { get; }
        public IAdminSetSolc SetSolc { get; }
        public IAdminStartRPC StartRPC { get; }
        public IAdminStartWS StartWS { get; }
        public IAdminStopRPC StopRPC { get; }
        public IAdminStopWS StopWS { get; }
        public IAdminPeers Peers { get; }
    }
}