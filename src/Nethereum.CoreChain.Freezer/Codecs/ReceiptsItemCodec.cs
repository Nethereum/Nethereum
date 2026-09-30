using System;
using System.Collections.Generic;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain.Freezer.Codecs
{
    public sealed class ReceiptsItemCodec : IItemCodec<IReadOnlyList<ReceiptForStorage>>
    {
        public byte[] Encode(IReadOnlyList<ReceiptForStorage> item)
        {
            var encoded = new byte[item.Count][];
            for (var i = 0; i < item.Count; i++)
                encoded[i] = EncodeOne(item[i]);
            return RLP.RLP.EncodeList(encoded);
        }

        public IReadOnlyList<ReceiptForStorage> Decode(ReadOnlySpan<byte> bytes)
        {
            var list = (RLPCollection)RLP.RLP.Decode(bytes.ToArray());
            var result = new List<ReceiptForStorage>(list.Count);
            foreach (RLPCollection receiptRlp in list)
                result.Add(DecodeOne(receiptRlp));
            return result;
        }

        private static byte[] EncodeOne(ReceiptForStorage receipt)
        {
            var encodedLogs = new byte[receipt.Logs.Count][];
            for (var i = 0; i < receipt.Logs.Count; i++)
                encodedLogs[i] = LogEncoder.Current.Encode(receipt.Logs[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(receipt.PostStateOrStatus),
                RLP.RLP.EncodeElement(receipt.CumulativeGasUsed.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(encodedLogs));
        }

        private static ReceiptForStorage DecodeOne(RLPCollection receiptRlp)
        {
            var postStateOrStatus = receiptRlp[0].RLPData ?? Array.Empty<byte>();
            var cumulativeGasUsed = receiptRlp[1].RLPData.ToBigIntegerFromRLPDecoded();

            var logs = new List<Log>();
            foreach (RLPCollection logRlp in (RLPCollection)receiptRlp[2])
                logs.Add(LogEncoder.Current.Decode(logRlp.RLPData));

            return new ReceiptForStorage(postStateOrStatus, cumulativeGasUsed, logs);
        }
    }
}
