using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Nethereum.Freezer.FilterMaps
{
    public static class LogValueHasher
    {
        private const ulong Fnv1aOffsetBasis = 0xcbf29ce484222325UL;
        private const ulong Fnv1aPrime = 0x100000001b3UL;

        public static byte[] AddressValue(ReadOnlySpan<byte> address)
        {
            if (address.Length != 20) throw new ArgumentException("address must be exactly 20 bytes", nameof(address));
            return SHA256.HashData(address);
        }

        public static byte[] TopicValue(ReadOnlySpan<byte> topic)
        {
            if (topic.Length != 32) throw new ArgumentException("topic must be exactly 32 bytes", nameof(topic));
            return SHA256.HashData(topic);
        }

        public static long MaskedMapIndex(long mapIndex, int layer, FilterMapsParams p)
        {
            var logLayerDiff = Math.Min(layer * p.LogLayerDiff, p.LogMapsPerEpoch);
            var clearedBits = p.LogMapsPerEpoch - logLayerDiff;
            var mask = unchecked(0xFFFFFFFFu << clearedBits);
            return (uint)mapIndex & mask;
        }

        public static int RowIndex(long mapIndex, int layer, ReadOnlySpan<byte> value, FilterMapsParams p)
        {
            var maskedMapIndex = (uint)MaskedMapIndex(mapIndex, layer, p);

            Span<byte> hashed = stackalloc byte[value.Length + 8];
            value.CopyTo(hashed);
            BinaryPrimitives.WriteUInt32LittleEndian(hashed.Slice(value.Length, 4), maskedMapIndex);
            BinaryPrimitives.WriteUInt32LittleEndian(hashed.Slice(value.Length + 4, 4), (uint)layer);

            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(hashed, hash);

            var firstFour = BinaryPrimitives.ReadUInt32LittleEndian(hash.Slice(0, 4));
            return (int)(firstFour % (uint)p.MapHeight);
        }

        public static int ColumnIndex(long lvIndex, ReadOnlySpan<byte> value, FilterMapsParams p)
        {
            var hashBits = p.LogMapWidth - p.LogValuesPerMap;
            var hash = Fnv1a64(lvIndex, value);

            var highBand = (uint)(hash >> (64 - hashBits));
            var lowBand = (uint)hash >> (32 - hashBits);
            var bandOffset = highBand ^ lowBand;

            var mapOffset = (uint)((ulong)lvIndex % (ulong)p.ValuesPerMap) << hashBits;
            return (int)(mapOffset + bandOffset);
        }

        private static ulong Fnv1a64(long lvIndex, ReadOnlySpan<byte> value)
        {
            Span<byte> lvIndexLe = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(lvIndexLe, (ulong)lvIndex);

            var hash = Fnv1aOffsetBasis;
            hash = Fnv1aFold(hash, lvIndexLe);
            hash = Fnv1aFold(hash, value);
            return hash;
        }

        private static ulong Fnv1aFold(ulong hash, ReadOnlySpan<byte> bytes)
        {
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= Fnv1aPrime;
            }
            return hash;
        }
    }
}
