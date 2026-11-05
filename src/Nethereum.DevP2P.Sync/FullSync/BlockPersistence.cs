using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.FullSync
{
    internal static class BlockPersistence
    {
        internal static async Task PersistBodyAsync(
            IChainStoreBundle bundle,
            byte[] blockHash,
            BigInteger blockNumber,
            IList<BlockHeader> uncles,
            IList<Withdrawal> withdrawals,
            IReadOnlyList<ISignedTransaction> transactions)
        {
            await bundle.Uncles.SaveAsync(blockHash, uncles ?? new List<BlockHeader>()).ConfigureAwait(false);

            if (withdrawals != null)
                await bundle.Withdrawals.SaveAsync(blockHash, withdrawals).ConfigureAwait(false);

            if (transactions != null && transactions.Count > 0)
                await bundle.Transactions.SaveManyAsync(blockHash, blockNumber, transactions).ConfigureAwait(false);
        }

        internal static Task PersistBodyAsync(
            IChainStoreBundle bundle, byte[] blockHash, BigInteger blockNumber, BlockBody body)
            => PersistBodyAsync(
                bundle, blockHash, blockNumber, body?.Uncles, body?.Withdrawals, body?.Transactions);

        internal static async Task PersistReceiptsAsync(
            IChainStoreBundle bundle, PersistableBlock block, BigInteger blockNumber)
        {
            if (block.Receipts != null && block.Receipts.Count > 0)
                await bundle.Receipts.SaveManyAsync(block.Hash, blockNumber, block.Receipts).ConfigureAwait(false);

            if (bundle.Logs != null && block.Logs != null && block.Logs.Count > 0)
                await bundle.Logs.SaveManyLogsAsync(block.Logs, block.Hash, blockNumber).ConfigureAwait(false);

            if (bundle.Logs != null && block.Bloom != null)
                await bundle.Logs.SaveBlockBloomAsync(blockNumber, block.Bloom).ConfigureAwait(false);
        }
    }
}
