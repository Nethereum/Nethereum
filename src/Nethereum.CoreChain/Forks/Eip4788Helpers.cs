using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Forks
{
    public static class Eip4788Helpers
    {
        public static BigInteger ComputeTimestampSlot(BigInteger timestamp, int historyBufferLength)
        {
            return timestamp % historyBufferLength;
        }

        public static BigInteger ComputeRootSlot(BigInteger timestamp, int historyBufferLength)
        {
            return ComputeTimestampSlot(timestamp, historyBufferLength) + historyBufferLength;
        }

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
