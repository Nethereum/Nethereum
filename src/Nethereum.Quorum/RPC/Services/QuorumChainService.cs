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

        public IQuorumCanonicalHash CanonicalHash { get; private set; }
        public IQuorumIsBlockMaker IsBlockMaker { get; private set; }
        public IQuorumIsVoter IsVoter { get; private set; }
        public IQuorumMakeBlock MakeBlock { get; private set; }
        public IQuorumPauseBlockMaker PauseBlockMaker { get; private set; }
        public IQuorumResumeBlockMaker ResumeBlockMaker { get; private set; }
        public IQuorumVote Vote { get; private set; }
        public IQuorumNodeInfo NodeInfo { get; private set; }
}
}
