using Nethereum.EVM;

namespace Nethereum.CoreChain.Forks
{
    /// <summary>
    /// <see cref="IChainActivations"/> for chains that pin one hardfork
    /// for every block — AppChain, DevChain, isolated unit tests. Mainnet
    /// and Holesky use schedule-driven registrations
    /// (<see cref="MainnetChainActivations"/>) and must not use this.
    /// </summary>
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
