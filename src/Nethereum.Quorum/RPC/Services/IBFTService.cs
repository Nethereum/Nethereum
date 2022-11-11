using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.IBFT;
using Nethereum.RPC;

namespace Nethereum.Quorum.RPC.Services
{
    public class IBFTService : RpcClientWrapper, IIBFTService
    {
        public IBFTService(IClient client) : base(client)
        {
            Candidates = new IstanbulCandidates(client);
            Discard = new IstanbulDiscard(client);
            GetSignersFromBlock = new IstanbulGetSignersFromBlock(client);
            GetSignersFromBlockByHash = new IstanbulGetSignersFromBlockByHash(client);
            GetSnapshot = new IstanbulGetSnapshot(client);
            GetSnapshotAtHash = new IstanbulGetSnapshotAtHash(client);
            GetValidators = new IstanbulGetValidators(client);
            GetValidatorsAtHash = new IstanbulGetValidatorsAtHash(client);
            IsValidator = new IstanbulIsValidator(client);
            NodeAddress = new IstanbulNodeAddress(client);
            Propose = new IstanbulPropose(client);
            Status = new IstanbulStatus(client);
        }

        public IIstanbulCandidates Candidates { get; }
        public IIstanbulDiscard Discard { get; }
        public IIstanbulGetSignersFromBlock GetSignersFromBlock { get; }
        public IIstanbulGetSignersFromBlockByHash GetSignersFromBlockByHash { get; }
        public IIstanbulGetSnapshot GetSnapshot { get; }
        public IIstanbulGetSnapshotAtHash GetSnapshotAtHash { get; }
        public IIstanbulGetValidators GetValidators { get; }
        public IIstanbulGetValidatorsAtHash GetValidatorsAtHash { get; }
        public IIstanbulIsValidator IsValidator { get; }
        public IIstanbulNodeAddress NodeAddress { get; } 
        public IIstanbulPropose Propose { get; }
        public IIstanbulStatus Status { get; }
    }
}