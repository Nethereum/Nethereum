using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.CoreChain.Forks
{
    public static class Eip2935Helpers
    {
        public static BigInteger ComputeSlot(BigInteger parentBlockNumber, int historyServeWindow)
        {
            return parentBlockNumber % historyServeWindow;
        }

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
