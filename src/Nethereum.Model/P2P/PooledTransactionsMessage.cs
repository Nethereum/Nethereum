using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/68 GetPooledTransactions (0x09): requests full mempool tx bodies for hashes.
    /// Format: [requestId, [hash_0, hash_1, ...]]
    /// </summary>
    public class GetPooledTransactionsMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> Hashes { get; set; } = new();
    }

    public static class GetPooledTransactionsMessageEncoder
    {
        public static byte[] Encode(GetPooledTransactionsMessage msg)
        {
            var encodedHashes = new byte[msg.Hashes.Count][];
            for (int i = 0; i < msg.Hashes.Count; i++)
                encodedHashes[i] = RLP.RLP.EncodeElement(msg.Hashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedHashes)
            );
        }

        public static GetPooledTransactionsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var hashList = (RLPCollection)outer[1];
            var msg = new GetPooledTransactionsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var h in hashList)
                msg.Hashes.Add(h.RLPData);
            return msg;
        }
    }

    /// <summary>
    /// eth/68 PooledTransactions (0x0a): response to GetPooledTransactions.
    /// Format: [requestId, [tx_0, tx_1, ...]]
    /// Typed txs wrapped as RLP byte strings per EIP-2718.
    /// </summary>
    public class PooledTransactionsMessage
    {
        public ulong RequestId { get; set; }
        public List<ISignedTransaction> Transactions { get; set; } = new();
    }

    public static class PooledTransactionsMessageEncoder
    {
        public static byte[] Encode(PooledTransactionsMessage msg)
        {
            var encodedTxs = new byte[msg.Transactions.Count][];
            for (int i = 0; i < msg.Transactions.Count; i++)
                encodedTxs[i] = EncodeTx(msg.Transactions[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedTxs)
            );
        }

        public static PooledTransactionsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var txList = (RLPCollection)outer[1];
            var msg = new PooledTransactionsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var txRlp in txList)
                msg.Transactions.Add(TransactionFactory.CreateTransaction(txRlp.RLPData));
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
