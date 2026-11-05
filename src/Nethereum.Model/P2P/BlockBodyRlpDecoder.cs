using System;
using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    internal static class BlockBodyRlpDecoder
    {
        public const int NoTransactionBound = int.MaxValue;

        public static BlockBody ReadBody(RlpReader reader, bool allowBlobNetworkWrapper, int maxTransactions)
        {
            var scope = reader.ReadListHeader();
            var body = new BlockBody();
            ReadContent(reader, scope, body.Transactions, body.Uncles, out var withdrawals,
                allowBlobNetworkWrapper, maxTransactions);
            body.Withdrawals = withdrawals;
            reader.EnsureEnd(scope);
            return body;
        }

        public static void ReadContent(RlpReader reader, RlpListScope scope,
            List<ISignedTransaction> transactions, List<BlockHeader> uncles, out List<Withdrawal> withdrawals,
            bool allowBlobNetworkWrapper, int maxTransactions)
        {
            withdrawals = null;

            var txs = reader.ReadListHeader();
            if (maxTransactions != NoTransactionBound && reader.CountItems(txs) > maxTransactions)
                throw new InvalidOperationException("RLP block body holds more than " + maxTransactions + " transactions");
            while (!reader.IsAtEnd(txs))
                transactions.Add(TransactionFactory.ReadTransaction(reader, allowBlobNetworkWrapper));
            reader.EnsureEnd(txs);

            var uncleList = reader.ReadListHeader();
            var headerEncoder = BlockHeaderEncoder.Current;
            while (!reader.IsAtEnd(uncleList))
                uncles.Add(headerEncoder.Decode(reader));
            reader.EnsureEnd(uncleList);

            if (!reader.IsAtEnd(scope))
            {
                withdrawals = new List<Withdrawal>();
                var withdrawalList = reader.ReadListHeader();
                var withdrawalEncoder = WithdrawalEncoder.Current;
                while (!reader.IsAtEnd(withdrawalList))
                    withdrawals.Add(withdrawalEncoder.Decode(reader));
                reader.EnsureEnd(withdrawalList);
            }
        }
    }
}
