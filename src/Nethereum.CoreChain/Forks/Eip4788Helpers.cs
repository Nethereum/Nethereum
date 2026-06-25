using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Forks
{
    /// <summary>
    /// Shared implementation of the EIP-4788 beacon-root system-call writes.
    /// The <see cref="BlockExecutor"/> engine calls into here on every
    /// Cancun+ block, so producer and follower paths cannot drift on slot
    /// derivation or value encoding.
    /// </summary>
    public static class Eip4788Helpers
    {
        /// <summary>
        /// Ring-buffer slot holding the timestamp at index
        /// <c>timestamp % HistoryBufferLength</c>. Spec EIP-4788 §Specification.
        /// </summary>
        public static BigInteger ComputeTimestampSlot(BigInteger timestamp, int historyBufferLength)
        {
            return timestamp % historyBufferLength;
        }

        /// <summary>
        /// Ring-buffer slot holding the parent beacon block root at index
        /// <c>(timestamp % HistoryBufferLength) + HistoryBufferLength</c>.
        /// Spec EIP-4788 §Specification.
        /// </summary>
        public static BigInteger ComputeRootSlot(BigInteger timestamp, int historyBufferLength)
        {
            return ComputeTimestampSlot(timestamp, historyBufferLength) + historyBufferLength;
        }

        /// <summary>
        /// Apply the EIP-4788 ring-buffer write to the beacon roots contract:
        /// timestamp at slot N, raw 32-byte parent beacon block root at slot
        /// N + HistoryBufferLength. The parent beacon block root is written
        /// without a BigInteger round-trip so leading zero bytes are preserved
        /// (a beacon root is a fixed 32-byte digest; trimming alters its value).
        ///
        /// <para>When <paramref name="witnessReader"/> is non-null, the
        /// contract account, code, and pre-block slot values are read through
        /// it before the writes — surfacing them in any
        /// <c>WitnessRecordingStateReader</c> decorator so the Zisk guest
        /// sees the BeaconRoots contract when it replays this block via the
        /// guest's EVM execution path. Pass null on hot paths that do not
        /// need witness completeness (followers, sequencers).</para>
        /// </summary>
        public static async Task ApplyAsync(
            IStateStore stateStore,
            BigInteger timestamp,
            byte[] parentBeaconBlockRoot,
            IStateReader? witnessReader = null)
        {
            if (stateStore == null) throw new ArgumentNullException(nameof(stateStore));
            if (parentBeaconBlockRoot == null) throw new ArgumentNullException(nameof(parentBeaconBlockRoot));

            var timestampSlot = ComputeTimestampSlot(timestamp, Eip4788Constants.HistoryBufferLength);
            var rootSlot = ComputeRootSlot(timestamp, Eip4788Constants.HistoryBufferLength);

            if (witnessReader != null)
            {
                await WarmContractAndSlotsAsync(witnessReader, Eip4788Constants.BeaconRootsAddress,
                    timestampSlot, rootSlot).ConfigureAwait(false);
            }

            var timestampBytes = timestamp.ToByteArray(isUnsigned: true, isBigEndian: true).TrimZeroBytes();
            await stateStore.SaveStorageAsync(Eip4788Constants.BeaconRootsAddress, timestampSlot, timestampBytes);
            await stateStore.SaveStorageAsync(Eip4788Constants.BeaconRootsAddress, rootSlot, parentBeaconBlockRoot);
        }

        private static async Task WarmContractAndSlotsAsync(
            IStateReader reader, string contractAddress, params BigInteger[] slots)
        {
            // Read the account → code → slots through the reader so the
            // witness captures the contract presence + bytecode + prior slot
            // values. The reads are not used here; the side effect of going
            // through a recording decorator is what matters.
            await reader.GetBalanceAsync(contractAddress).ConfigureAwait(false);
            await reader.GetTransactionCountAsync(contractAddress).ConfigureAwait(false);
            await reader.GetCodeAsync(contractAddress).ConfigureAwait(false);
            foreach (var slot in slots)
            {
                var slotKey = EvmUInt256BigIntegerExtensions.FromBigInteger(slot);
                await reader.GetStorageAtAsync(contractAddress, slotKey).ConfigureAwait(false);
            }
        }
    }
}
