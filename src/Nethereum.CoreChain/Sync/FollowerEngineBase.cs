using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Sync
{
    internal abstract class FollowerEngineBase
    {
        protected static bool IsCursorOwnedByStagedFlush(IChainStoreBundle bundle, ulong blockNumber)
            => bundle is IAtomicBlockFlush atomicFlush && atomicFlush.BlockOwnedByStagedFlush(blockNumber);

        protected static async Task CommitCursorIfBehindAsync(IChainStoreBundle bundle, ulong blockNumber, byte[] headerHash)
        {
            if (IsCursorOwnedByStagedFlush(bundle, blockNumber)) return;
            if (bundle is IAtomicBlockFlush atomicFlush)
                await atomicFlush.DrainAsync().ConfigureAwait(false);
            if (bundle.Metadata.GetLastBlock() < blockNumber)
                bundle.Metadata.Commit(blockNumber, headerHash);
        }

        protected static async Task CommitCursorAtRunExitAsync(
            IChainStoreBundle bundle, ulong lastCommittedBlock, byte[] lastCommittedHash, ILogger logger)
        {
            if (bundle != null && lastCommittedBlock > 0)
            {
                try
                {
                    await CommitCursorIfBehindAsync(bundle, lastCommittedBlock, lastCommittedHash).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex,
                        "FollowerService finally-block Commit({block}, {hash}) failed; metadata may be stale on next start.",
                        lastCommittedBlock,
                        lastCommittedHash != null ? lastCommittedHash.ToHex() : "(null)");
                }
            }
        }

        protected async Task CommitMatchedBlockAsync(
            IChainStoreBundle bundle, ulong blockNumber, byte[] headerHash, BlockBundle blockBundle,
            byte[] computedStateRoot, FollowerOptions options, ILogger logger, CancellationToken ct)
        {
            await CommitCursorIfBehindAsync(bundle, blockNumber, headerHash).ConfigureAwait(false);
            bool alreadyFolded = bundle is IAtomicBlockFlush atomicFlush && atomicFlush.WithdrawalsFoldedFor(blockNumber);
            if (!alreadyFolded && blockBundle.Withdrawals != null && blockBundle.Withdrawals.Count > 0)
                await bundle.Withdrawals.SaveAsync(headerHash, blockBundle.Withdrawals).ConfigureAwait(false);

            await CheckpointPolicy.MaybeCheckpointAsync(
                bundle, blockNumber, computedStateRoot, headerHash, options, logger, ct)
                .ConfigureAwait(false);
        }
    }
}
