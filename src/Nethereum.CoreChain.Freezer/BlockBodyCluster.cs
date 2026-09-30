using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer
{
    public sealed class BlockBodyCluster
    {
        public IReadOnlyList<ISignedTransaction> Txs { get; }
        public IReadOnlyList<BlockHeader> Uncles { get; }
        public IReadOnlyList<Withdrawal> Withdrawals { get; }

        public BlockBodyCluster(
            IReadOnlyList<ISignedTransaction> txs,
            IReadOnlyList<BlockHeader> uncles,
            IReadOnlyList<Withdrawal> withdrawals)
        {
            Txs = txs ?? new List<ISignedTransaction>();
            Uncles = uncles ?? new List<BlockHeader>();
            Withdrawals = withdrawals;
        }
    }
}
