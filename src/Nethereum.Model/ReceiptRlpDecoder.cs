using System;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    internal static class ReceiptRlpDecoder
    {
        private const int BloomBytes = 256;
        private const int PostStateBytes = 32;
        private const byte MaxTypeByte = 0x7f;
        private const byte LastSupportedTypedReceipt = 4;

        public static Receipt Decode(byte[] rawdata)
        {
            if (rawdata == null || rawdata.Length == 0)
                return null;

            if (rawdata[0] <= MaxTypeByte)
                return DecodeTyped(rawdata);

            var reader = new RlpReader(rawdata);
            var receipt = ReadConsensusList(reader);
            reader.EnsureEnd();
            return receipt;
        }

        public static Receipt Read(RlpReader reader)
        {
            if (reader.PeekKind() == RlpKind.List)
                return ReadConsensusList(reader);

            return DecodeTyped(reader.ReadBytes());
        }

        public static Receipt ReadEth69(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var transactionType = reader.ReadUInt64();
            if (transactionType > MaxTypeByte)
                throw new InvalidOperationException("Receipt transaction type is above 0x7f");

            var receipt = new Receipt
            {
                TransactionType = (byte)transactionType,
                PostStateOrStatus = ReadPostStateOrStatus(reader),
                CumulativeGasUsed = new EvmUInt256(reader.ReadUInt64()),
                Logs = LogEncoder.Current.DecodeList(reader)
            };
            reader.EnsureEnd(scope);

            var bloom = new LogBloomFilter();
            foreach (var log in receipt.Logs)
                bloom.AddLog(log);
            receipt.Bloom = bloom.Data;
            return receipt;
        }

        private static Receipt DecodeTyped(byte[] typed)
        {
            if (typed.Length < 2 || typed[0] == 0 || typed[0] > MaxTypeByte)
                throw new InvalidOperationException("A typed receipt is a byte string that opens with a type byte from 1 to 0x7f and holds a receipt list");
            if (typed[0] > LastSupportedTypedReceipt)
                throw new InvalidOperationException("Receipt transaction type " + typed[0] + " is not supported");

            var reader = new RlpReader(typed, 1, typed.Length - 1);
            var receipt = ReadConsensusList(reader);
            reader.EnsureEnd();
            receipt.TransactionType = typed[0];
            return receipt;
        }

        private static Receipt ReadConsensusList(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var receipt = new Receipt
            {
                PostStateOrStatus = ReadPostStateOrStatus(reader),
                CumulativeGasUsed = new EvmUInt256(reader.ReadUInt64()),
                Bloom = reader.ReadFixed(BloomBytes),
                Logs = LogEncoder.Current.DecodeList(reader)
            };
            reader.EnsureEnd(scope);
            return receipt;
        }

        private static byte[] ReadPostStateOrStatus(RlpReader reader)
        {
            var value = reader.ReadBytes();
            var isStatus = value.Length == 0 || (value.Length == 1 && value[0] == 1);
            if (!isStatus && value.Length != PostStateBytes)
                throw new InvalidOperationException("Receipt postStateOrStatus is " + value.Length + " bytes");
            return value;
        }
    }
}
