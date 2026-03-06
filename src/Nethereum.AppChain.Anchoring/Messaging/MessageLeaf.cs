using System;
using System.Linq;
using Nethereum.Util;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageLeaf
    {
        public ulong SourceChainId { get; set; }
        public ulong MessageId { get; set; }
        public byte[] AppChainTxHash { get; set; } = Array.Empty<byte>();
        public bool Success { get; set; }
        public byte[] DataHash { get; set; } = Array.Empty<byte>();

        public byte[] ComputeLeafHash()
        {
            return ComputeHash(SourceChainId, MessageId, AppChainTxHash, Success, DataHash);
        }

        public byte[] GetEncodedData()
        {
            return EncodeLeafData(SourceChainId, MessageId, AppChainTxHash, Success, DataHash);
        }

        public static byte[] ComputeHash(
            ulong sourceChainId,
            ulong messageId,
            byte[] txHash,
            bool success,
            byte[] dataHash)
        {
            var encoded = EncodeLeafData(sourceChainId, messageId, txHash, success, dataHash);
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        public static byte[] EncodeLeafData(
            ulong sourceChainId,
            ulong messageId,
            byte[] txHash,
            bool success,
            byte[] dataHash)
        {
            var result = new byte[8 + 8 + 32 + 1 + 32];
            WriteUInt64BigEndian(result, 0, sourceChainId);
            WriteUInt64BigEndian(result, 8, messageId);

            var txHashPadded = PadOrTruncate(txHash, 32);
            Buffer.BlockCopy(txHashPadded, 0, result, 16, 32);

            result[48] = success ? (byte)1 : (byte)0;

            var dataHashPadded = PadOrTruncate(dataHash, 32);
            Buffer.BlockCopy(dataHashPadded, 0, result, 49, 32);

            return result;
        }

        private static void WriteUInt64BigEndian(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)(value >> 56);
            buffer[offset + 1] = (byte)(value >> 48);
            buffer[offset + 2] = (byte)(value >> 40);
            buffer[offset + 3] = (byte)(value >> 32);
            buffer[offset + 4] = (byte)(value >> 24);
            buffer[offset + 5] = (byte)(value >> 16);
            buffer[offset + 6] = (byte)(value >> 8);
            buffer[offset + 7] = (byte)value;
        }

        private static byte[] PadOrTruncate(byte[] data, int length)
        {
            if (data == null || data.Length == 0)
                return new byte[length];
            if (data.Length == length)
                return data;
            if (data.Length > length)
            {
                var result = new byte[length];
                Buffer.BlockCopy(data, 0, result, 0, length);
                return result;
            }
            else
            {
                var result = new byte[length];
                Buffer.BlockCopy(data, 0, result, length - data.Length, data.Length);
                return result;
            }
        }
    }
}
