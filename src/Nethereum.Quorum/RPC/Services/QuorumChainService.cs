using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Quorum.RPC.Services
{
    public class QuorumChainService : RpcClientWrapper, IQuorumChainService
    {
        public QuorumChainService(IClient client) : base(client)
        {
            CanonicalHash = new QuorumCanonicalHash(client);  
            IsBlockMaker = new QuorumIsBlockMaker(client);
            IsVoter = new QuorumIsVoter(client);
            MakeBlock = new QuorumMakeBlock(client);
            PauseBlockMaker = new QuorumPauseBlockMaker(client);
            ResumeBlockMaker = new QuorumResumeBlockMaker(client);
            Vote = new QuorumVote(client);  
            NodeInfo = new QuorumNodeInfo(client);
        }

        public IQuorumCanonicalHash CanonicalHash { get; }
        public IQuorumIsBlockMaker IsBlockMaker { get; }
        public IQuorumIsVoter IsVoter { get; }
        public IQuorumMakeBlock MakeBlock { get; }
        public IQuorumPauseBlockMaker PauseBlockMaker { get; }
        public IQuorumResumeBlockMaker ResumeBlockMaker { get; }
        public IQuorumVote Vote { get; }
        public IQuorumNodeInfo NodeInfo { get; }
}
}
