using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface ISideBlockStore
    {
        void Put(
            byte[] hash,
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<AccountChanges>? declaredBlockAccessList);

        bool TryGet(byte[] hash, out SideBlock block);

        bool Contains(byte[] hash);

        void Remove(byte[] hash);

        void PruneBelow(ulong blockNumber);
    }

    public sealed class SideBlock
    {
        public SideBlock(
            byte[] hash,
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<AccountChanges>? declaredBlockAccessList)
        {
            Hash = hash;
            Header = header;
            Transactions = transactions;
            Uncles = uncles;
            Withdrawals = withdrawals;
            DeclaredBlockAccessList = declaredBlockAccessList;
        }

        public byte[] Hash { get; }
        public BlockHeader Header { get; }
        public IList<ISignedTransaction> Transactions { get; }
        public IList<BlockHeader>? Uncles { get; }
        public IList<Withdrawal>? Withdrawals { get; }
        public List<AccountChanges>? DeclaredBlockAccessList { get; }
    }
}
