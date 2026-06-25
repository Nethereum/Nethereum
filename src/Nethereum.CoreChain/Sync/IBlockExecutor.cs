using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Single-call abstraction over <see cref="BlockImporter.ImportAsync"/>.
    /// Provided so the follower can be tested with a stub returning
    /// scripted <see cref="BlockImporterResult"/>s without requiring a
    /// fully-wired chain (genesis alloc, hardfork registry, state trie).
    /// Production callers supply <see cref="BlockImporter"/> directly.
    /// </summary>
    public interface IBlockExecutor
    {
        Task<BlockImporterResult> ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<WithdrawalEntry> withdrawals,
            CancellationToken ct);
    }
}
