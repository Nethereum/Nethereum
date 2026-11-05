using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface IMempoolReorgReconciler
    {
        Task ReconcileAsync(
            IChainStoreBundle bundle,
            ulong commonAncestor,
            ulong orphanedHead,
            IReadOnlyList<ISignedTransaction> winningBranchTransactions,
            CancellationToken ct);

        Task ReconcileAsync(
            IChainStoreBundle bundle,
            ulong commonAncestor,
            ulong orphanedHead,
            IReadOnlyList<ISignedTransaction> orphanedBranchTransactions,
            IReadOnlyList<ISignedTransaction> winningBranchTransactions,
            CancellationToken ct)
            => ReconcileAsync(bundle, commonAncestor, orphanedHead, winningBranchTransactions, ct);
    }
}
