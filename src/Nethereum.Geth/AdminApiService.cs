using Nethereum.Geth.RPC.Admin;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.Web3;

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
        }

        public AdminAddPeer AddPeer { get; private set; }
        public AdminDatadir Datadir { get; private set; }
        public AdminNodeInfo NodeInfo { get; private set; }
        public AdminSetSolc SetSolc { get; private set; }
        public AdminStartRPC StartRPC { get; private set; }
        public AdminStartWS StartWS { get; private set; }
        public AdminStopRPC StopRPC { get; private set; }
        public AdminStopWS StopWS { get; private set; }
    }
}