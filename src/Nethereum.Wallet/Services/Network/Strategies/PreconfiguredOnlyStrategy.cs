using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network.Strategies
{
    /// <summary>
    /// Uses only the supplied preconfigured features. No external enrichment.
    /// </summary>
    public class PreconfiguredOnlyStrategy : ChainFeatureStrategyBase
    {
        private readonly Dictionary<BigInteger, ChainFeature> _preconfigured;

        public PreconfiguredOnlyStrategy(
            IEnumerable<ChainFeature> preconfiguredFeatures,
            IEnumerable<BigInteger>? defaultChainIds = null)
            : base(defaultChainIds ?? preconfiguredFeatures.Select(f => f.ChainId))
        {
            _preconfigured = (preconfiguredFeatures ?? Enumerable.Empty<ChainFeature>())
                .GroupBy(c => c.ChainId)
                .ToDictionary(g => g.Key, g => Clone(g.First()));
        }

        public override Task<ChainFeature?> ResolveChainAsync(BigInteger chainId) =>
            Task.FromResult(_preconfigured.TryGetValue(chainId, out var f) ? Clone(f) : null);

        public override Task<List<ChainFeature>> GetDefaultChainsAsync() =>
            Task.FromResult(DefaultChainIds
                .Where(id => _preconfigured.ContainsKey(id))
                .Select(id => Clone(_preconfigured[id]))
                .ToList());
    }
}