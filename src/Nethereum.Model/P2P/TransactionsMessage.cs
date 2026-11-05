using System;
using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/68 Transactions (0x02): announces new mempool transactions to peers.
    /// Format: [tx_0, tx_1, ...]
    /// Typed transactions (EIP-2718) are wrapped as RLP byte strings; legacy
    /// transactions are RLP lists.
    /// </summary>
    public class TransactionsMessage
    {
        public List<ISignedTransaction> Transactions { get; set; } = new();
    }

    public static class TransactionsMessageEncoder
    {
        public static byte[] Encode(TransactionsMessage msg)
        {
            var encodedTxs = new byte[msg.Transactions.Count][];
            for (int i = 0; i < msg.Transactions.Count; i++)
                encodedTxs[i] = EncodeTx(msg.Transactions[i]);
            return RLP.RLP.EncodeList(encodedTxs);
        }

        public static TransactionsMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            if (reader.CountItems(outer) > PeerResponseBounds.MaxTransactionAnnouncements)
                throw new InvalidOperationException("Transactions: too many transactions");
            var msg = new TransactionsMessage();
            var seen = new HashSet<string>();
            while (!reader.IsAtEnd(outer))
            {
                var tx = TransactionFactory.ReadTransaction(reader, allowBlobNetworkWrapper: false);
                if (tx is Transaction4844)
                    throw new InvalidOperationException("Transactions: disallowed broadcast blob transaction");
                PeerRlp.AddUnique(seen, tx);
                msg.Transactions.Add(tx);
            }
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }

        private static byte[] EncodeTx(ISignedTransaction tx)
        {
            var raw = tx.GetRLPEncoded();
            if (raw.Length > 0 && raw[0] < 0xc0)
                return RLP.RLP.EncodeElement(raw);
            return raw;
        }
    }
}
