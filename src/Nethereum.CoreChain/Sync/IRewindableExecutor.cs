using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Services;

namespace Nethereum.CoreChain.Sync
{
    internal interface IRewindableExecutor
    {
        Task<RewindToAncestorResult> RewindToAncestorAsync(
            IRewindCoordinator rewind, ulong ancestorNumber, RewindScope scope, CancellationToken ct);
    }

    internal sealed class RewindToAncestorResult
    {
        public RewindToAncestorResult(bool rewound, RewindResult rewind)
        {
            Rewound = rewound;
            Rewind = rewind;
        }

        public bool Rewound { get; }
        public RewindResult Rewind { get; }
    }

    internal static class AncestorRewind
    {
        internal static async Task<RewindToAncestorResult> RunAsync(
            IRewindCoordinator rewind,
            ulong ancestorNumber,
            RewindScope scope,
            Action discardPendingState,
            ILogger logger,
            CancellationToken ct)
        {
            var result = await rewind
                .RewindToAsync(ancestorNumber, RewindPolicy.JournalFirstThenSnapshot, scope, ct)
                .ConfigureAwait(false);

            var viaJournal = result.Outcome == RewindOutcome.JournalUsed
                             || result.Outcome == RewindOutcome.NodeHistoryUsed
                             || result.Outcome == RewindOutcome.NoOp;
            if (!viaJournal)
            {
                logger.LogError(
                    "reorg.disaster_recovery ancestor={Ancestor} outcome={Outcome} checkpoint={Checkpoint} detail={Detail}",
                    ancestorNumber, result.Outcome, result.RestoredCheckpoint?.BlockNumber, result.Detail);
                return new RewindToAncestorResult(false, result);
            }

            if (result.NewHead != ancestorNumber) return new RewindToAncestorResult(false, result);

            discardPendingState?.Invoke();
            return new RewindToAncestorResult(true, result);
        }
    }
}
