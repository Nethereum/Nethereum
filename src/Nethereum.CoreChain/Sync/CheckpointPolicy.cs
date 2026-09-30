using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Sync
{
    internal static class CheckpointPolicy
    {
        public static async Task MaybeCheckpointAsync(
            IChainStoreBundle bundle, ulong committedBlock, byte[] stateRoot, byte[] committedHash,
            FollowerOptions options, ILogger logger, CancellationToken ct)
        {
            if (options.CheckpointEvery <= 0 || committedBlock == 0 || committedBlock % options.CheckpointEvery != 0)
                return;

            bool checkpointSaved = false;
            try
            {
                await bundle.SaveCheckpointAsync(committedBlock, stateRoot, committedHash, ct).ConfigureAwait(false);
                checkpointSaved = true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception cpEx)
            {
                logger.LogWarning(cpEx,
                    "Checkpoint save failed at block {block}; sync continues without this checkpoint.",
                    committedBlock);
            }

            if (checkpointSaved && options.KeepLatestCheckpoints.HasValue && options.KeepLatestCheckpoints.Value > 0)
                await PruneOlderCheckpointsAsync(bundle, options.KeepLatestCheckpoints.Value, logger, ct).ConfigureAwait(false);
        }

        private static async Task PruneOlderCheckpointsAsync(
            IChainStoreBundle bundle, int keepLatest, ILogger logger, CancellationToken ct)
        {
            var existing = bundle.Metadata.ListCheckpointBlockNumbers();
            if (existing.Count <= keepLatest) return;

            int dropCount = existing.Count - keepLatest;
            for (int i = 0; i < dropCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                ulong bn = existing[i];
                try
                {
                    await bundle.DeleteCheckpointAsync(bn).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    logger.LogWarning(ex,
                        "Auto-prune: DeleteCheckpointAsync({block}) failed; sync continues.", bn);
                }
            }
            logger.LogInformation(
                "Auto-prune: dropped {dropped} checkpoint(s) below the latest {keep}.",
                dropCount, keepLatest);
        }
    }
}
