using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network.Strategies
{
    public abstract class ChainFeatureStrategyBase : IChainFeatureSourceStrategy
    {
        protected readonly IReadOnlyCollection<BigInteger> DefaultChainIds;
        protected readonly IExternalChainFeaturesProvider? External;

        protected ChainFeatureStrategyBase(
            IEnumerable<BigInteger> defaultChainIds,
            IExternalChainFeaturesProvider? external = null)
        {
            if (defaultChainIds == null)
                throw new ArgumentNullException(nameof(defaultChainIds));

            var ids = defaultChainIds
                .Where(i => i > BigInteger.Zero)
                .Distinct()
                .OrderBy(i => (long)i)
                .ToArray();

            if (ids.Length == 0)
                throw new ArgumentException("At least one default chain id must be provided.", nameof(defaultChainIds));

            DefaultChainIds = ids;
            External = external;
        }

        public abstract Task<ChainFeature?> ResolveChainAsync(BigInteger chainId);
        public abstract Task<List<ChainFeature>> GetDefaultChainsAsync();

        public virtual Task<bool> RefreshChainAsync(BigInteger chainId) =>
            External?.RefreshAsync(chainId) ?? Task.FromResult(false);

        protected static ChainFeature Clone(ChainFeature c) => new()
        {
            ChainId = c.ChainId,
            ChainName = c.ChainName,
            IsTestnet = c.IsTestnet,
            NativeCurrency = c.NativeCurrency == null ? null : new NativeCurrency
            {
                Name = c.NativeCurrency.Name,
                Symbol = c.NativeCurrency.Symbol,
                Decimals = c.NativeCurrency.Decimals
            },
            SupportEIP155 = c.SupportEIP155,
            SupportEIP1559 = c.SupportEIP1559,
            HttpRpcs = c.HttpRpcs?.ToList() ?? new List<string>(),
            WsRpcs = c.WsRpcs?.ToList() ?? new List<string>(),
            Explorers = c.Explorers?.ToList() ?? new List<string>()
        };

        protected static ChainFeature EnrichIfMissing(ChainFeature baseFeature, ChainFeature enrichment)
        {
            var clone = Clone(baseFeature);

            if (clone.HttpRpcs == null || clone.HttpRpcs.Count == 0)
                clone.HttpRpcs = enrichment.HttpRpcs?.ToList() ?? new List<string>();

            if (clone.Explorers == null || clone.Explorers.Count == 0)
                clone.Explorers = enrichment.Explorers?.ToList() ?? new List<string>();

            return clone;
        }
    }
}