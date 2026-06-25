using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
    /// <summary>
    /// Tries multiple <see cref="ICanonicalStateRootSource"/> implementations
    /// in declaration order and returns the first one that has an answer.
    /// Lets operators stack sources by trust + cost: prefer the cheapest /
    /// most authoritative first, fall back to broader / slower sources.
    ///
    /// <para>
    /// Example: <c>new CompositeCanonicalStateRootSource(appChainAnchor,
    /// beaconFinality, rpcFallback)</c> — read the L1 anchor (free, but
    /// only at anchored heights); if absent ask the consensus client for
    /// finality (free, only finalised heights); if absent ask a trusted
    /// RPC node (cheap, any height).
    /// </para>
    /// </summary>
    public sealed class CompositeCanonicalStateRootSource : ICanonicalStateRootSource
    {
        private readonly IReadOnlyList<ICanonicalStateRootSource> _sources;

        public CompositeCanonicalStateRootSource(params ICanonicalStateRootSource[] sources)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("Composite needs at least one inner source.", nameof(sources));
            foreach (var s in sources)
                if (s == null) throw new ArgumentException("Composite inner source must not be null.", nameof(sources));
            _sources = sources;
        }

        public string Name => string.Join(" -> ", _sources.Select(s => s.Name));

        /// <summary>The inner sources in priority order. Observability layers
        /// (e.g. snap-sync progress reporter watching for beacon staleness)
        /// walk this list to find the source they want to probe.</summary>
        public IReadOnlyList<ICanonicalStateRootSource> Sources => _sources;

        public async Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct)
        {
            foreach (var source in _sources)
            {
                ct.ThrowIfCancellationRequested();
                var (root, hash) = await source.GetCanonicalAsync(blockNumber, ct).ConfigureAwait(false);
                if (root != null) return (root, hash);
            }
            return (null, null);
        }

        public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
        {
            foreach (var source in _sources)
            {
                ct.ThrowIfCancellationRequested();
                var tip = await source.GetLatestAsync(ct).ConfigureAwait(false);
                if (tip != null) return tip;
            }
            return null;
        }
    }
}
