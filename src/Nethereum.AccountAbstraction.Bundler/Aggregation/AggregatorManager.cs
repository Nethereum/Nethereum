using System.Collections.Concurrent;
using Nethereum.AccountAbstraction.Interfaces;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Aggregation
{
    public class AggregatorManager : IAggregatorManager
    {
        private readonly ConcurrentDictionary<string, IAggregator> _aggregators = new();

        public bool SupportsAggregation => _aggregators.Count > 0;

        public IReadOnlyCollection<string> RegisteredAggregators => _aggregators.Keys.ToList().AsReadOnly();

        public void RegisterAggregator(string aggregatorAddress, IAggregator aggregator)
        {
            if (string.IsNullOrEmpty(aggregatorAddress))
                throw new ArgumentNullException(nameof(aggregatorAddress));
            if (aggregator == null)
                throw new ArgumentNullException(nameof(aggregator));

            _aggregators[aggregatorAddress.ToLowerInvariant()] = aggregator;
        }

        public IAggregator? GetAggregator(string aggregatorAddress)
        {
            if (string.IsNullOrEmpty(aggregatorAddress))
                return null;

            _aggregators.TryGetValue(aggregatorAddress.ToLowerInvariant(), out var aggregator);
            return aggregator;
        }

        public string? DetectAggregator(PackedUserOperation userOp)
        {
            if (userOp.Signature == null || userOp.Signature.Length < BlsAggregator.COMBINED_SIZE)
                return null;

            foreach (var (address, aggregator) in _aggregators)
            {
                if (aggregator is BlsAggregator blsAggregator)
                {
                    if (BlsAggregator.IsBlsSignature(userOp.Signature))
                    {
                        return address;
                    }
                }
            }

            return null;
        }
    }
}
