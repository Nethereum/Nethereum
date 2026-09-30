using System;
using System.Buffers.Binary;

namespace Nethereum.CoreChain.Storage.History
{
    public static class HistoryKeys
    {
        public const int BlockKeyLength = 8;

        public const int TxKeyLength = 12;

        public static byte[] BlockKey(ulong blockNumber)
        {
            var key = new byte[BlockKeyLength];
            BinaryPrimitives.WriteUInt64BigEndian(key, blockNumber);
            return key;
        }

        public static byte[] TxKey(ulong blockNumber, uint txIndex)
        {
            var key = new byte[TxKeyLength];
            BinaryPrimitives.WriteUInt64BigEndian(key.AsSpan(0, 8), blockNumber);
            BinaryPrimitives.WriteUInt32BigEndian(key.AsSpan(8, 4), txIndex);
            return key;
        }

        public static ulong ReadBlockNumber(ReadOnlySpan<byte> key)
        {
            if (key.Length < BlockKeyLength)
                throw new ArgumentException($"key must be at least {BlockKeyLength} bytes", nameof(key));
            return BinaryPrimitives.ReadUInt64BigEndian(key.Slice(0, 8));
        }

        public static uint ReadTxIndex(ReadOnlySpan<byte> key)
        {
            if (key.Length < TxKeyLength)
                throw new ArgumentException($"tx key must be at least {TxKeyLength} bytes", nameof(key));
            return BinaryPrimitives.ReadUInt32BigEndian(key.Slice(8, 4));
        }
    }
}
