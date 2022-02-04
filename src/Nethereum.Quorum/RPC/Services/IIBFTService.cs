using Nethereum.Quorum.RPC.IBFT;

namespace Nethereum.Quorum.RPC.Services
{
    public interface IIBFTService
    {
        IIstanbulCandidates Candidates { get; }
        IIstanbulDiscard Discard { get; }
        IIstanbulGetSignersFromBlock GetSignersFromBlock { get; }
        IIstanbulGetSignersFromBlockByHash GetSignersFromBlockByHash { get; }
        IIstanbulGetSnapshot GetSnapshot { get; }
        IIstanbulGetSnapshotAtHash GetSnapshotAtHash { get; }
        IIstanbulGetValidators GetValidators { get; }
        IIstanbulGetValidatorsAtHash GetValidatorsAtHash { get; }
        IIstanbulIsValidator IsValidator { get; }
        IIstanbulNodeAddress NodeAddress { get; }
        IIstanbulPropose Propose { get; }
        IIstanbulStatus Status { get; }
    }
}