using System.Collections.Generic;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class ReceiptEncoder
    {
        public static ReceiptEncoder Current { get; } = new ReceiptEncoder();

        public byte[] Encode(Receipt receipt)
        {
            var encodedLogs = new byte[receipt.Logs.Count][];
            for (int i = 0; i < receipt.Logs.Count; i++)
                encodedLogs[i] = LogEncoder.Current.Encode(receipt.Logs[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(receipt.PostStateOrStatus ?? RLP.RLP.EMPTY_BYTE_ARRAY),
                RLP.RLP.EncodeElement(receipt.CumulativeGasUsed.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(receipt.Bloom ?? new byte[256]),
                RLP.RLP.EncodeList(encodedLogs)
            );
        }

        public byte[] EncodeTyped(Receipt receipt, byte transactionType)
        {
            var encoded = Encode(receipt);
            var result = new byte[encoded.Length + 1];
            result[0] = transactionType;
            encoded.CopyTo(result, 1);
            return result;
        }

        public Receipt Decode(byte[] rawdata)
        {
            return ReceiptRlpDecoder.Decode(rawdata);
        }

        public Receipt Decode(RlpReader reader)
        {
            return ReceiptRlpDecoder.Read(reader);
        }
    }
}
