using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class HealerFactory
    {
        internal static Nethereum.CoreChain.Storage.IHealNodeSink CreateSink(IChainStoreBundle bundle)
            => (bundle as Nethereum.CoreChain.Storage.IHealNodeSinkProvider)?.CreateHealSink()
               ?? new Nethereum.CoreChain.Storage.HashHealNodeSink((INodeBlobStore)bundle.TrieNodes);

        internal static TrieHealer Create(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, ILogger logger, SnapSyncMetrics? metrics,
            PivotWatch pivots, IReadOnlyCollection<byte[]>? accountsNeedingHeal = null)
        {
            var sink = CreateSink(bundle);
            var healer = new TrieHealer(scheduler, sink, bundle.StateTrieNodes, logger, metrics,
                flatWriter: sink as Nethereum.CoreChain.Storage.ISnapFlatStateWriter
                            ?? bundle.State as Nethereum.CoreChain.Storage.ISnapFlatStateWriter,
                codeStore: bundle.State);
            healer.AccountsNeedingHeal = accountsNeedingHeal;
            if (pivots?.Refresher != null)
                healer.PivotRefresher = pivots.AsHealerRefresher();
            return healer;
        }

        internal static Task<TrieHealer.HealResult> HealAtAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, ILogger logger, SnapSyncMetrics? metrics,
            PivotWatch pivots, SnapBootstrapper.PivotState target, IReadOnlyCollection<byte[]>? accountsNeedingHeal,
            IReadOnlyList<byte[]>? codeSeeds, CancellationToken ct)
            => Create(bundle, scheduler, logger, metrics, pivots, accountsNeedingHeal)
                .HealAsync(target.Header.StateRoot, seedCodeHeal: codeSeeds, pivotBlock: (ulong)target.Header.BlockNumber, ct: ct);
    }
}
