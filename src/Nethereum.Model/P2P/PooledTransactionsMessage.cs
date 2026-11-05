using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new GetPooledTransactionsMessage
            {
                RequestId = reader.ReadUInt64()
            };
            msg.Hashes.AddRange(PeerRlp.ReadHashes(reader));
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new PooledTransactionsMessage { RequestId = reader.ReadUInt64() };
            var transactions = reader.ReadListHeader();
            var seen = new HashSet<string>();
            while (!reader.IsAtEnd(transactions))
            {
                var tx = TransactionFactory.ReadTransaction(reader);
                PeerRlp.AddUnique(seen, tx);
                msg.Transactions.Add(tx);
            }
            reader.EnsureEnd(transactions);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }

        private static byte[] EncodeTx(ISignedTransaction tx)
        {
            // EIP-4844: PooledTransactions serves the networking (with-sidecar)
            // form of a type-3 blob tx — blobs, commitments and proofs — so the
            // requesting peer can verify and re-propagate it. Non-blob txs are
            // unchanged. Decode reconstructs the sidecar via TransactionFactory.
            var raw = tx is Transaction4844 blobTx
                ? blobTx.GetRLPEncodedWithSidecar()
                : tx.GetRLPEncoded();
            if (raw.Length > 0 && raw[0] < 0xc0)
                return RLP.RLP.EncodeElement(raw);
            return raw;
        }
    }
}
