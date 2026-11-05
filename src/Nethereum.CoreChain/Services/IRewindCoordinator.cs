using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.Services
{
    public interface IRewindCoordinator
    {
        Task<RewindResult> RewindToAsync(
            ulong targetBlock,
            RewindPolicy policy,
            CancellationToken ct = default);

        Task<RewindResult> RewindToAsync(
            ulong targetBlock,
            RewindPolicy policy,
            RewindScope scope,
            CancellationToken ct = default)
            => RewindToAsync(targetBlock, policy, ct);
    }
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "RewindScope - whether a rewind also truncates the chain data")]
    public enum RewindScope
    {
        ExecutionAndChain,
        ExecutionOnly,
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "RewindPolicy - which rewind paths the coordinator may use")]
    public enum RewindPolicy
    {
        JournalFirstThenSnapshot,
        JournalOnly,
        SnapshotOnly,
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "RewindOutcome - which rewind path actually got there")]
    public enum RewindOutcome
    {
        NoOp,
        JournalUsed,
        NodeHistoryUsed,
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
