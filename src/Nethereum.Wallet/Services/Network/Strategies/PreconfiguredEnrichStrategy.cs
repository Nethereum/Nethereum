using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network.Strategies
{
    /// <summary>
    /// Returns preconfigured chains; if missing RPCs / explorers, attempts to enrich from external.
    /// If a chain exists only externally and not preconfigured, it will still be returned for resolution,
    /// but default list only includes IDs in DefaultChainIds (union of explicit IDs passed in).
    /// </summary>
    public class PreconfiguredEnrichStrategy : ChainFeatureStrategyBase
    {
        private readonly Dictionary<BigInteger, ChainFeature> _preconfigured;

        public PreconfiguredEnrichStrategy(
            IEnumerable<ChainFeature> preconfiguredFeatures,
            IExternalChainFeaturesProvider? external,
            IEnumerable<BigInteger>? defaultChainIds = null)
            : base(defaultChainIds ?? preconfiguredFeatures.Select(f => f.ChainId), external)
        {
            _preconfigured = (preconfiguredFeatures ?? Enumerable.Empty<ChainFeature>())
                .GroupBy(c => c.ChainId)
                .ToDictionary(g => g.Key, g => Clone(g.First()));
        }

        public override async Task<ChainFeature?> ResolveChainAsync(BigInteger chainId)
        {
            _preconfigured.TryGetValue(chainId, out var pre);

            if (pre == null)
            {
                if (External == null) return null;
                return await External.GetExternalChainAsync(chainId).ConfigureAwait(false);
            }

            if (External == null) return Clone(pre);

            var external = await External.GetExternalChainAsync(chainId).ConfigureAwait(false);
            if (external == null) return Clone(pre);

            return EnrichIfMissing(pre, external);
        }

        public override async Task<List<ChainFeature>> GetDefaultChainsAsync()
        {
            if (External == null)
            {
                return DefaultChainIds
                    .Where(id => _preconfigured.ContainsKey(id))
                    .Select(id => Clone(_preconfigured[id]))
                    .ToList();
            }

            var externalList = await External.GetExternalChainsAsync(DefaultChainIds).ConfigureAwait(false);
            var externalMap = externalList.Where(c => c != null)
                .ToDictionary(c => c.ChainId, c => c);

            var result = new List<ChainFeature>();
            foreach (var id in DefaultChainIds)
            {
                _preconfigured.TryGetValue(id, out var pre);
                externalMap.TryGetValue(id, out var ext);

                if (pre != null && ext != null)
                {
                    result.Add(EnrichIfMissing(pre, ext));
                }
                else if (pre != null)
                {
                    result.Add(Clone(pre));
                }
                else if (ext != null)
                {
                    // Not preconfigured but still part of explicit default ids (if provided)
                    result.Add(Clone(ext));
                }
            }
            return result;
        }
    }
}