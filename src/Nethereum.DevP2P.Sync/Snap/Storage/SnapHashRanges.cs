using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    public static class SnapHashRanges
    {
        public static List<(byte[] Start, byte[] End)> SplitHashRange(byte[] from, byte[] to, int chunks)
        {
            var fromBig = new BigInteger(from, isUnsigned: true, isBigEndian: true);
            var toBig = new BigInteger(to, isUnsigned: true, isBigEndian: true);
            var span = toBig - fromBig + 1;
            if (span <= chunks)
                return new List<(byte[], byte[])> { (from, to) };
            var step = span / chunks;
            var ranges = new List<(byte[] Start, byte[] End)>(chunks);
            var cursor = fromBig;
            for (int i = 0; i < chunks; i++)
            {
                var end = i == chunks - 1 ? toBig : cursor + step - 1;
                ranges.Add((BigToHash(cursor), BigToHash(end)));
                cursor = end + 1;
            }
            return ranges;
        }

        public static byte[] BigToHash(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32) return bytes;
            var padded = new byte[32];
            System.Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        public static byte[] IncrementHash(byte[] h)
        {
            var copy = (byte[])h.Clone();
            for (int i = copy.Length - 1; i >= 0; i--)
            {
                if (copy[i] != 0xff) { copy[i]++; return copy; }
                copy[i] = 0;
            }
            return copy;
        }

        public static byte[] FilledHash(byte b)
        {
            var hh = new byte[32];
            for (int i = 0; i < 32; i++) hh[i] = b;
            return hh;
        }

        private static readonly BigInteger MaxBig256 = (BigInteger.One << 256) - 1;

        public static ulong? EstimateRemainingSlots(int hashesInPage, byte[] last)
        {
            if (last == null || last.Length != 32) return null;
            var lastBig = new BigInteger(last, isUnsigned: true, isBigEndian: true);
            if (lastBig.IsZero) return null;

            var space = MaxBig256 * hashesInPage / lastBig;
            if (space > ulong.MaxValue) return null;

            return (ulong)space - (ulong)hashesInPage;
        }

        public static int ComputeWhaleChunkCount(int maxChunks, ulong maxRequestSizeBytes, int hashesInPage, byte[] lastSlotHash)
        {
            var estimate = EstimateRemainingSlots(hashesInPage, lastSlotHash);
            if (estimate == null) return maxChunks;

            var slotsPerPacket = maxRequestSizeBytes / 64UL;
            var slotsPerChunk = 2 * slotsPerPacket;
            if (slotsPerChunk == 0) return maxChunks;

            var chunks = estimate.Value / slotsPerChunk + 1;
            return chunks < (ulong)maxChunks ? (int)chunks : maxChunks;
        }
    }
}
