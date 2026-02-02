using Nethereum.AccountAbstraction.Interfaces;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Aggregation
{
    public interface IAggregatorManager
    {
        void RegisterAggregator(string aggregatorAddress, IAggregator aggregator);

        IAggregator? GetAggregator(string aggregatorAddress);

        string? DetectAggregator(PackedUserOperation userOp);

        bool SupportsAggregation { get; }

        IReadOnlyCollection<string> RegisteredAggregators { get; }
    }
}
