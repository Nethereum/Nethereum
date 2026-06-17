using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Services
{
    /// <summary>
    /// Composes the two existing rewind primitives — journal replay
    /// (<see cref="StateRewindService"/>) and snapshot restore
    /// (<see cref="IChainStoreBundle.RestoreCheckpointAsync"/>) — behind a
    /// single decision API. The coordinator owns the policy; it does not
    /// own bundle lifecycle. A <see cref="RewindOutcome.SnapshotUsed"/>
    /// result is a recommendation: the caller, which holds the bundle
    /// reference and the open backend handle, performs the dispose,
    /// snapshot restore, reopen, and journal truncation itself.
    /// </summary>
    public interface IRewindCoordinator
    {
        Task<RewindResult> RewindToAsync(
            ulong targetBlock,
            RewindPolicy policy,
            CancellationToken ct = default);
    }

    public enum RewindPolicy
    {
        JournalFirstThenSnapshot,
        JournalOnly,
        SnapshotOnly,
    }

    public enum RewindOutcome
    {
        NoOp,
        JournalUsed,
        SnapshotUsed,
        NoPathAvailable,
    }

    public sealed record RewindResult(
        RewindOutcome Outcome,
        ulong NewHead,
        ulong? UndoneCount,
        ChainCheckpoint? RestoredCheckpoint,
        string Detail);
}
