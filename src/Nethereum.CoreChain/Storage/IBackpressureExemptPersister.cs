using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public interface IBackpressureExemptPersister
    {
        System.Numerics.BigInteger FreezeBoundary { get; }

        Task<int> PersistBackpressureExemptPrefixAsync(
            IReadOnlyList<PersistableBlock> blocks, System.Numerics.BigInteger freezeBoundary, CancellationToken ct = default);
    }
}
