using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
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
