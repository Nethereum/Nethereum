using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Result = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.Result;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class Phase4Finalize
    {
        internal static async Task<Result> FinishAsync(
            SnapRunContext context, Phase1History phase1,
            Func<Task<BlockHeader>> finalizeCore,
            Func<(int Accounts, int Slots, int Bytecodes)> counts,
            CancellationToken ct)
        {
            var bundle = context.Bundle;
            var logger = context.Logger;
            Task historyBackfill = phase1.FirstCompletion ?? phase1.HandOffToBackground(ct);

            BlockHeader finalPivotHeader;
            try
            {
                finalPivotHeader = await finalizeCore().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await Phase1History.StopAsync(phase1.BackfillCts, historyBackfill, logger).ConfigureAwait(false);
                throw;
            }

            var (accounts, slots, bytecodes) = counts();
            return new Result
            {
                Ran = true,
                PivotBlockNumber = (ulong)finalPivotHeader.BlockNumber,
                PivotStateRoot = finalPivotHeader.StateRoot,
                AccountCount = accounts,
                SlotCount = slots,
                BytecodeCount = bytecodes,
                HistoryBackfill = historyBackfill,
            };
        }

        internal static async Task<BlockHeader> CompleteAsync(
            SnapRunContext context, byte[] pivotHash, ulong pivotNumber, bool backfillRan, bool phase3Entered, CancellationToken ct)
        {
            return await PivotCommit.PersistAsync(
                context.Bundle, pivotHash, pivotNumber, backfillRan, phase3Entered, context.Logger, ct)
                .ConfigureAwait(false);
        }

        internal static void FlushBulk(IBulkFlatStateSink bulkFlat, IChainStoreBundle bundle)
        {
            bulkFlat?.Flush();
            (bundle as IBulkDurabilityBoundary)?.CheckpointBulk();
        }
    }
}
