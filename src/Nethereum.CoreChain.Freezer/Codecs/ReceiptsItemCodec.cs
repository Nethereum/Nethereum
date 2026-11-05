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
            var reader = new RlpReader(bytes.ToArray());
            var list = reader.ReadListHeader();
            var result = new List<ReceiptForStorage>();
            while (!reader.IsAtEnd(list))
                result.Add(DecodeOne(reader));
            reader.EnsureEnd(list);
            reader.EnsureEnd();
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

        private static ReceiptForStorage DecodeOne(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var postStateOrStatus = reader.ReadBytes();
            var cumulativeGasUsed = new System.Numerics.BigInteger(reader.ReadUInt64());
            var logs = LogEncoder.Current.DecodeList(reader);
            reader.EnsureEnd(scope);
            return new ReceiptForStorage(postStateOrStatus, cumulativeGasUsed, logs);
        }
    }
}
