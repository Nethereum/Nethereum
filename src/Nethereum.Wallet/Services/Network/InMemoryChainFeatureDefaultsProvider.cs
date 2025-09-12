using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network
{
    /// <summary>
    /// Simple in-memory provider; pass a list of immutable (or safely cloned) ChainFeature defaults.
    /// Returned features are cloned to avoid external mutation side-effects.
    /// </summary>
    public class InMemoryChainFeatureDefaultsProvider : IChainFeatureDefaultsProvider
    {
        private readonly Dictionary<BigInteger, ChainFeature> _defaults;

        public InMemoryChainFeatureDefaultsProvider(IEnumerable<ChainFeature> defaults)
        {
            _defaults = (defaults ?? Enumerable.Empty<ChainFeature>())
                .GroupBy(c => c.ChainId)
                .ToDictionary(g => g.Key, g => Clone(g.First()));
        }

        public IEnumerable<ChainFeature> GetDefaultChainFeatures() =>
            _defaults.Values.Select(Clone);

        private static ChainFeature Clone(ChainFeature original) => new()
        {
            ChainId = original.ChainId,
            ChainName = original.ChainName,
            IsTestnet = original.IsTestnet,
            NativeCurrency = original.NativeCurrency == null ? null : new NativeCurrency
            {
                Name = original.NativeCurrency.Name,
                Symbol = original.NativeCurrency.Symbol,
                Decimals = original.NativeCurrency.Decimals
            },
            SupportEIP155 = original.SupportEIP155,
            SupportEIP1559 = original.SupportEIP1559,
            HttpRpcs = original.HttpRpcs?.ToList() ?? new List<string>(),
            WsRpcs = original.WsRpcs?.ToList() ?? new List<string>(),
            Explorers = original.Explorers?.ToList() ?? new List<string>()
        };
    }
}