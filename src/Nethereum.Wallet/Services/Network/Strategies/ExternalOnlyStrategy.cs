using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network.Strategies
{
    /// <summary>
    /// Sources everything from the external provider only (e.g. ChainList).
    /// </summary>
    public class ExternalOnlyStrategy : ChainFeatureStrategyBase
    {
        public ExternalOnlyStrategy(
            IEnumerable<BigInteger> defaultChainIds,
            IExternalChainFeaturesProvider external)
            : base(defaultChainIds, external) { }

        public override Task<ChainFeature?> ResolveChainAsync(BigInteger chainId) =>
            External?.GetExternalChainAsync(chainId) ?? Task.FromResult<ChainFeature?>(null);

        public override async Task<List<ChainFeature>> GetDefaultChainsAsync()
        {
            if (External == null) return new List<ChainFeature>();
            var list = await External.GetExternalChainsAsync(DefaultChainIds).ConfigureAwait(false);
            var result = new List<ChainFeature>(list.Count);
            foreach (var c in list)
            {
                if (c != null) result.Add(Clone(c));
            }
            return result;
        }
    }
}