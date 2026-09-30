using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.Sync
{
    public interface IFollowerService
    {
        Task<FollowerRunResult> RunAsync(
            IBlockSource source,
            Func<IChainStoreBundle> bundleFactory,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            CancellationToken ct,
            ILogger logger = null);
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "FollowerOptions - how the follower loop is paced and bounded")]
    public sealed record FollowerOptions(
        ulong StartBlock,
        ulong CheckpointEvery,
        ulong AnchorEvery,
        int MaxConsecutiveDivergences = 3,
        int MaxRewindCycles = 3,
        ulong? EndBlock = null,
        int MaxConsecutiveSourceFailures = 120,
        int? KeepLatestCheckpoints = null,
        TimeSpan? TipPollInterval = null,
        ulong WalkerInvocationThreshold = 1UL,
        ulong MaxReorgDepth = 1024UL,
        bool ExternalHeaderFollow = false);

    public sealed record FollowerRunResult(
        FollowerExitReason ExitReason,
        ulong LastExecutedBlock,
        ulong BlocksExecuted,
        ulong RootMismatches,
        ulong RewindCyclesUsed,
        ChainCheckpoint? SnapshotRestoreTarget,
        string Detail);

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "FollowerExitReason - why the follower loop returned")]
    public enum FollowerExitReason
    {
        SourceCompleted,
        Cancelled,
        FatalVerdict,
        RewindUnavailable,
        SnapshotRestoreRequested,
        SourceUnavailable,
    }
}
