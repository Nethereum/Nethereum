using System.Collections.Generic;
using System.Linq;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public class ReceiptEncoder
    {
        public static ReceiptEncoder Current { get; } = new ReceiptEncoder();

        public byte[] Encode(Receipt receipt)
        {
            var encodedLogs = receipt.Logs
                .Select(log => LogEncoder.Current.Encode(log))
                .ToArray();

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
            if (rawdata == null || rawdata.Length == 0)
                return null;

            if (rawdata[0] <= 0x7f)
            {
                var innerData = new byte[rawdata.Length - 1];
                System.Array.Copy(rawdata, 1, innerData, 0, innerData.Length);
                return DecodeRlp(innerData);
            }

            return DecodeRlp(rawdata);
        }

        private Receipt DecodeRlp(byte[] rawdata)
        {
            var decodedList = RLP.RLP.Decode(rawdata);
            var decodedElements = (RLPCollection)decodedList;

            var receipt = new Receipt();
            receipt.PostStateOrStatus = decodedElements[0].RLPData;
            receipt.CumulativeGasUsed = decodedElements[1].RLPData.ToBigIntegerFromRLPDecoded();
            receipt.Bloom = decodedElements[2].RLPData;

            var logsCollection = (RLPCollection)decodedElements[3];
            receipt.Logs = new List<Log>();
            foreach (var logData in logsCollection)
            {
                receipt.Logs.Add(LogEncoder.Current.Decode(logData.RLPData));
            }

            return receipt;
        }
    }
}
