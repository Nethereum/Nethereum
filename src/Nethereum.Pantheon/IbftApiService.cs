using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.RPC.IBFT;
using Nethereum.RPC;

namespace Nethereum.Pantheon
{
    public interface IIbftApiService
    {
        IIbftDiscardValidatorVote DiscardValidatorVote { get; }
        IIbftGetValidatorsByBlockHash GetValidatorsByBlockHash { get; }
        IIbftGetPendingVotes GetPendingVotes { get; }
        IIbftGetValidatorsByBlockNumber GetValidatorsByBlockNumber { get; }
        IIbftProposeValidatorVote ProposeValidatorVote { get; }
    }

    public class IbftApiService : RpcClientWrapper, IIbftApiService
    {
        public IbftApiService(IClient client) : base(client)
        {
            DiscardValidatorVote = new IbftDiscardValidatorVote(client);
            GetValidatorsByBlockHash = new IbftGetValidatorsByBlockHash(client);
            GetPendingVotes = new IbftGetPendingVotes(client);
            GetValidatorsByBlockNumber = new IbftGetValidatorsByBlockNumber(client);
            ProposeValidatorVote = new IbftProposeValidatorVote(client);
        }

        public IIbftDiscardValidatorVote DiscardValidatorVote { get; }
        public IIbftGetValidatorsByBlockHash GetValidatorsByBlockHash { get; }
        public IIbftGetPendingVotes GetPendingVotes { get; }
        public IIbftGetValidatorsByBlockNumber GetValidatorsByBlockNumber { get; }
        public IIbftProposeValidatorVote ProposeValidatorVote { get; }
    
    }
}