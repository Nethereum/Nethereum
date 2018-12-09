namespace Nethereum.Quorum.RPC.Services
{
    public interface IQuorumChainService
    {
        IQuorumCanonicalHash CanonicalHash { get; }
        IQuorumIsBlockMaker IsBlockMaker { get; }
        IQuorumIsVoter IsVoter { get; }
        IQuorumMakeBlock MakeBlock { get; }
        IQuorumNodeInfo NodeInfo { get; }
        IQuorumPauseBlockMaker PauseBlockMaker { get; }
        IQuorumResumeBlockMaker ResumeBlockMaker { get; }
        IQuorumVote Vote { get; }
    }
}