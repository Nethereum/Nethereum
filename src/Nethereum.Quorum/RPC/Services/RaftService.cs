using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.Raft;
using Nethereum.RPC;

namespace Nethereum.Quorum.RPC.Services
{
    public class RaftService : RpcClientWrapper, IRaftService
    {
        public RaftService(IClient client) : base(client)
        {
            AddLearner = new RaftAddLearner(client);
            AddPeer = new RaftAddPeer(client);
            AddCluster = new RaftCluster(client);
            Leader = new RaftLeader(client);
            PromoteToPeer = new RaftPromoteToPeer(client);
            RemovePeer = new RaftRemovePeer(client);
            Role = new RaftRole(client);
        }

        public IRaftAddLearner AddLearner { get; }
        public IRaftAddPeer AddPeer { get; }
        public IRaftCluster AddCluster { get; }
        public IRaftLeader Leader { get; }
        public IRaftPromoteToPeer PromoteToPeer { get; }
        public IRaftRemovePeer RemovePeer { get; }
        public IRaftRole Role { get; }
    }
}