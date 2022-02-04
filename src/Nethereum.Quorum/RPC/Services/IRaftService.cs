using Nethereum.Quorum.RPC.Raft;

namespace Nethereum.Quorum.RPC.Services
{
    public interface IRaftService
    {
        IRaftAddLearner AddLearner { get; }
        IRaftAddPeer AddPeer { get; }
        IRaftCluster AddCluster { get; }
        IRaftLeader Leader { get; }
        IRaftPromoteToPeer PromoteToPeer { get; }
        IRaftRemovePeer RemovePeer { get; }
        IRaftRole Role { get; }
    }
}