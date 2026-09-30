using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IBatchedBlockPersister
    {
        Task PersistBlocksAsync(IReadOnlyList<PersistableBlock> blocks, CancellationToken ct = default);
    }

    public sealed class PersistableBlock
    {
        public PersistableBlock(
            BlockHeader header,
            byte[] hash,
            IList<BlockHeader> uncles,
            IList<Withdrawal> withdrawals,
            IReadOnlyList<ISignedTransaction> transactions,
            IReadOnlyList<ReceiptSaveItem> receipts,
            IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> logs,
            byte[] bloom)
        {
            Header = header;
            Hash = hash;
            Uncles = uncles;
            Withdrawals = withdrawals;
            Transactions = transactions;
            Receipts = receipts;
            Logs = logs;
            Bloom = bloom;
        }

        public BlockHeader Header { get; }
        public byte[] Hash { get; }
        public IList<BlockHeader> Uncles { get; }
        public IList<Withdrawal> Withdrawals { get; }
        public IReadOnlyList<ISignedTransaction> Transactions { get; }
        public IReadOnlyList<ReceiptSaveItem> Receipts { get; }
        public IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> Logs { get; }
        public byte[] Bloom { get; }
    }
}
