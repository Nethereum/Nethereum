using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.CoreChain.Forks
{
    /// <summary>
    /// Shared implementation of the EIP-2935 history-contract system-call
    /// write. The <see cref="BlockExecutor"/> engine calls into here on
    /// every Prague+ block, so producer and follower paths cannot drift on
    /// slot derivation or value encoding.
    /// </summary>
    public static class Eip2935Helpers
    {
        /// <summary>
        /// Ring-buffer slot holding the parent block hash for the block being
        /// processed. Per EIP-2935 the slot is
        /// <c>parentBlockNumber % HistoryServeWindow</c> — i.e. the slot is
        /// keyed on the block whose hash is stored, not on the consuming block.
        /// </summary>
        public static BigInteger ComputeSlot(BigInteger parentBlockNumber, int historyServeWindow)
        {
            return parentBlockNumber % historyServeWindow;
        }

        /// <summary>
        /// Apply the EIP-2935 ring-buffer write: raw 32-byte parent block hash
        /// at the slot derived from the parent block number. Written without a
        /// BigInteger round-trip so leading zero bytes are preserved (a block
        /// hash is a fixed 32-byte digest; trimming alters its value).
        ///
        /// <para>When <paramref name="witnessReader"/> is non-null, the
        /// contract account, code, and pre-block slot value are read through
        /// it before the write — surfacing them in any
        /// <c>WitnessRecordingStateReader</c> decorator so the Zisk guest
        /// sees the HistoryStorage contract when it replays this block via
        /// the guest's EVM execution path. Pass null on hot paths that do
        /// not need witness completeness (followers, sequencers).</para>
        /// </summary>
        public static async Task ApplyAsync(
            IStateStore stateStore,
            BigInteger parentBlockNumber,
            byte[] parentBlockHash,
            IStateReader? witnessReader = null)
        {
            if (stateStore == null) throw new ArgumentNullException(nameof(stateStore));
            if (parentBlockHash == null) throw new ArgumentNullException(nameof(parentBlockHash));

            var slot = ComputeSlot(parentBlockNumber, Eip2935Constants.HistoryServeWindow);

            if (witnessReader != null)
            {
                await witnessReader.GetBalanceAsync(Eip2935Constants.HistoryStorageAddress).ConfigureAwait(false);
                await witnessReader.GetTransactionCountAsync(Eip2935Constants.HistoryStorageAddress).ConfigureAwait(false);
                await witnessReader.GetCodeAsync(Eip2935Constants.HistoryStorageAddress).ConfigureAwait(false);
                var slotKey = EvmUInt256BigIntegerExtensions.FromBigInteger(slot);
                await witnessReader.GetStorageAtAsync(Eip2935Constants.HistoryStorageAddress, slotKey).ConfigureAwait(false);
            }

            await stateStore.SaveStorageAsync(Eip2935Constants.HistoryStorageAddress, slot, parentBlockHash);
        }
    }
}
