using Nethereum.EVM;

namespace Nethereum.CoreChain.Forks
{
    public sealed class FixedChainActivations : IChainActivations
    {
        private readonly HardforkName _fork;

        public FixedChainActivations(HardforkName fork)
        {
            _fork = fork;
        }

        public HardforkName ResolveAt(long blockNumber, ulong timestamp) => _fork;
    }
}
