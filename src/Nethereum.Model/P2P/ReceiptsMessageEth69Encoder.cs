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
                encodedBlocks[i] = EncodeBlockReceipts(msg.ReceiptsByBlock[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedBlocks));
        }

        /// <summary>
        /// Encodes one block's receipt list in the eth/69 (EIP-7642) shape —
        /// bloom-stripped, transaction type moved inside each receipt as the
        /// first element: <c>[[txType, postStateOrStatus, gasUsed, logs], ...]</c>.
        /// This is the body eth/70's Receipts envelope reuses unchanged
        /// (only the outer request-id/flag wrapper differs); callers that
        /// need the standalone per-block byte size (e.g. a response-byte
        /// soft cap) can use <c>EncodeBlockReceipts(receipts).Length</c>.
        /// </summary>
        public static byte[] EncodeBlockReceipts(List<Receipt> blockReceipts)
        {
            var encodedReceipts = new byte[blockReceipts.Count][];
            for (int j = 0; j < blockReceipts.Count; j++)
                encodedReceipts[j] = EncodeSingleReceipt(blockReceipts[j]);
            return RLP.RLP.EncodeList(encodedReceipts);
        }

        public static byte[] EncodeSingleReceipt(Receipt r)
        {
            var encodedLogs = new byte[r.Logs.Count][];
            for (int k = 0; k < r.Logs.Count; k++)
                encodedLogs[k] = LogEncoder.Current.Encode(r.Logs[k]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)r.TransactionType).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(r.PostStateOrStatus ?? RLP.RLP.EMPTY_BYTE_ARRAY),
                RLP.RLP.EncodeElement(r.CumulativeGasUsed.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedLogs));
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
                if (PeerResponseBounds.ExceedsWhatABlockCanHold(blockReceipts)) break;
                msg.ReceiptsByBlock.Add(DecodeBlockReceipts(blockReceipts));
            }

            return msg;
        }

        public static List<Receipt> DecodeBlockReceipts(RLPCollection blockReceipts)
        {
            var receipts = new List<Receipt>();
            foreach (RLPCollection receiptFields in blockReceipts)
                receipts.Add(DecodeSingleReceipt(receiptFields));
            return receipts;
        }

        public static Receipt DecodeSingleReceipt(RLPCollection receiptFields)
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

            return receipt;
        }
    }
}
