using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Strategy that turns a raw mempool snapshot into an ordered list of
    /// <see cref="TxEntry"/> ready for <see cref="BlockExecutor.ExecuteAsync"/>.
    /// Sequencer-owned: <see cref="BlockExecutor"/> itself trusts whatever
    /// order it gets and never reorders. Implementations may filter (drop
    /// nonce-gapped txs, drop over-gas-limit txs), reorder (mempool-nonce
    /// grouping, fee-priority), and cap (don't exceed gas budget).
    /// </summary>
    public interface ITransactionOrderingPolicy
    {
        /// <summary>
        /// Order and filter <paramref name="pool"/> for execution in the
        /// block described by <paramref name="blockContext"/>. Returned
        /// list is the exact sequence the engine will execute; cached
        /// senders are forwarded to
        /// <see cref="TransactionProcessor.ExecuteTransactionAsync"/> via
        /// <see cref="TxEntry.CachedSender"/>.
        /// </summary>
        IReadOnlyList<TxEntry> Order(
            IEnumerable<ISignedTransaction> pool,
            BlockContext blockContext,
            BigInteger gasLimit,
            CancellationToken ct);
    }
}
