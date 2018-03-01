using Nethereum.Geth.RPC.Admin;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Geth
{
    public class AdminApiService : RpcClientWrapper
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

        public AdminAddPeer AddPeer { get; }
        public AdminDatadir Datadir { get; }
        public AdminNodeInfo NodeInfo { get; }
        public AdminSetSolc SetSolc { get; }
        public AdminStartRPC StartRPC { get; }
        public AdminStartWS StartWS { get; }
        public AdminStopRPC StopRPC { get; }
        public AdminStopWS StopWS { get; }
        public AdminPeers Peers { get; }
    }
}