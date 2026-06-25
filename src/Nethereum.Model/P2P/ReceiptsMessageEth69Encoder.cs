using System.Collections.Generic;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/69 network encoding of a Receipts (0x10) message body.
    /// Per EIP-7642, the bloom filter is dropped from the wire format and the
    /// transaction type is moved inside the per-receipt list as the first
    /// element. The on-wire shape of a single receipt is therefore
    /// <c>[txType, postStateOrStatus, gasUsed, logs]</c> rather than the
    /// eth/68 shape <c>[postStateOrStatus, gasUsed, bloom, logs]</c> wrapped
    /// in an outer <c>txType||rlp</c> byte string for non-legacy receipts.
    /// </summary>
    public static class ReceiptsMessageEth69Encoder
    {
        public static byte[] Encode(ReceiptsMessage msg)
        {
            var encodedBlocks = new byte[msg.ReceiptsByBlock.Count][];
            for (int i = 0; i < msg.ReceiptsByBlock.Count; i++)
            {
                var blockReceipts = msg.ReceiptsByBlock[i];
                var encodedReceipts = new byte[blockReceipts.Count][];
                for (int j = 0; j < blockReceipts.Count; j++)
                {
                    var r = blockReceipts[j];
                    var encodedLogs = new byte[r.Logs.Count][];
                    for (int k = 0; k < r.Logs.Count; k++)
                        encodedLogs[k] = LogEncoder.Current.Encode(r.Logs[k]);

                    encodedReceipts[j] = RLP.RLP.EncodeList(
                        RLP.RLP.EncodeElement(((long)r.TransactionType).ToBytesForRLPEncoding()),
                        RLP.RLP.EncodeElement(r.PostStateOrStatus ?? RLP.RLP.EMPTY_BYTE_ARRAY),
                        RLP.RLP.EncodeElement(r.CumulativeGasUsed.ToBytesForRLPEncoding()),
                        RLP.RLP.EncodeList(encodedLogs));
                }
                encodedBlocks[i] = RLP.RLP.EncodeList(encodedReceipts);
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedBlocks));
        }

        public static ReceiptsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new ReceiptsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };

            var blockList = (RLPCollection)outer[1];
            foreach (RLPCollection blockReceipts in blockList)
            {
                var receipts = new List<Receipt>();
                foreach (RLPCollection receiptFields in blockReceipts)
                {
                    var txTypeBytes = receiptFields[0].RLPData ?? new byte[0];
                    var receipt = new Receipt
                    {
                        TransactionType = txTypeBytes.Length == 0 ? (byte)0 : txTypeBytes[txTypeBytes.Length - 1],
                        PostStateOrStatus = receiptFields[1].RLPData ?? new byte[0],
                        CumulativeGasUsed = receiptFields[2].RLPData.ToEvmUInt256FromRLPDecoded()
                    };

                    var logsCollection = (RLPCollection)receiptFields[3];
                    receipt.Logs = new List<Log>();
                    foreach (var logData in logsCollection)
                    {
                        receipt.Logs.Add(LogEncoder.Current.Decode(logData.RLPData));
                    }

                    // EIP-7642: eth/69 strips the bloom from the wire and
                    // expects the receiver to recompute it from the logs.
                    // Without this, downstream receipt-root computation
                    // (which encodes via ReceiptEncoder.Encode and includes
                    // the bloom field) sees a zeroed bloom and produces the
                    // wrong trie root.
                    var bloom = new LogBloomFilter();
                    foreach (var log in receipt.Logs)
                    {
                        bloom.AddLog(log);
                    }
                    receipt.Bloom = bloom.Data;

                    receipts.Add(receipt);
                }
                msg.ReceiptsByBlock.Add(receipts);
            }

            return msg;
        }
    }
}
