using System;
using System.Linq;

namespace Nethereum.EVM.ForkId
{
    public readonly struct Eip2124ForkId
    {
        public uint Hash { get; }
        public ulong Next { get; }

        public Eip2124ForkId(uint hash, ulong next)
        {
            Hash = hash;
            Next = next;
        }
    }

    public enum Eip2124ValidationResult
    {
        Accepted,

        RemoteStale,

        LocalIncompatibleOrStale
    }

    public static class Eip2124ForkIdCalculator
    {
        public static uint ComputeForkHash(byte[] genesisHash, ulong[] forkBlocks, ulong[] forkTimestamps)
        {
            if (genesisHash == null || genesisHash.Length != 32)
                throw new ArgumentException("genesisHash must be 32 bytes", nameof(genesisHash));

            uint crc = 0xffffffff;
            crc = Crc32IeeeUpdate(crc, genesisHash);

            foreach (var fb in SortDedupNonZero(forkBlocks))
                crc = Crc32IeeeUpdate(crc, Uint64Be(fb));
            foreach (var ts in SortDedupNonZero(forkTimestamps))
                crc = Crc32IeeeUpdate(crc, Uint64Be(ts));

            return crc ^ 0xffffffff;
        }

        public static Eip2124ForkId NewId(
            byte[] genesisHash, ulong[] forkBlocks, ulong[] forkTimestamps, ulong headBlock, ulong headTime)
        {
            var boundaries = OrderedBoundaries(forkBlocks, forkTimestamps);
            var passedBlockCount = 0;
            var passedTimestampCount = 0;
            ulong next = 0;

            foreach (var boundary in boundaries)
            {
                var passed = boundary.IsBlock ? headBlock >= boundary.Value : headTime >= boundary.Value;
                if (!passed)
                {
                    next = boundary.Value;
                    break;
                }
                if (boundary.IsBlock) passedBlockCount++; else passedTimestampCount++;
            }

            var passedBlocks = boundaries.Where(b => b.IsBlock).Take(passedBlockCount).Select(b => b.Value).ToArray();
            var passedTimestamps = boundaries.Where(b => !b.IsBlock).Take(passedTimestampCount).Select(b => b.Value).ToArray();
            var hash = ComputeForkHash(genesisHash, passedBlocks, passedTimestamps);
            return new Eip2124ForkId(hash, next);
        }

        public static Eip2124ValidationResult ValidateForkId(
            uint remoteHash, ulong remoteNext,
            byte[] genesisHash, ulong[] forkBlocks, ulong[] forkTimestamps,
            ulong headBlock, ulong headTime)
        {
            var boundaries = OrderedBoundaries(forkBlocks, forkTimestamps);
            var sums = ChecksumPrefixSums(genesisHash, boundaries);
            var firstUnpassedIndex = FirstUnpassedBoundaryIndex(boundaries, headBlock, headTime);

            if (sums[firstUnpassedIndex] == remoteHash)
                return RemoteAnnouncedAFutureForkWeAlreadyPassed(remoteNext, headBlock, headTime, boundaries, firstUnpassedIndex)
                    ? Eip2124ValidationResult.LocalIncompatibleOrStale
                    : Eip2124ValidationResult.Accepted;

            for (var j = 0; j < firstUnpassedIndex; j++)
            {
                if (sums[j] != remoteHash) continue;
                return boundaries[j].Value == remoteNext
                    ? Eip2124ValidationResult.Accepted
                    : Eip2124ValidationResult.RemoteStale;
            }

            for (var j = firstUnpassedIndex + 1; j < sums.Length; j++)
            {
                if (sums[j] == remoteHash) return Eip2124ValidationResult.Accepted;
            }

            return Eip2124ValidationResult.LocalIncompatibleOrStale;
        }

        private const ulong TimestampThreshold = 1_438_269_973UL;

        private static bool RemoteAnnouncedAFutureForkWeAlreadyPassed(
            ulong remoteNext, ulong headBlock, ulong headTime, ForkBoundary[] boundaries, int firstUnpassedIndex)
        {
            if (remoteNext == 0) return false;
            var weAreAtTimestampBoundaries = firstUnpassedIndex >= boundaries.Length || boundaries[firstUnpassedIndex].IsBlock == false;
            var head = weAreAtTimestampBoundaries ? headTime : headBlock;
            return head >= remoteNext || (remoteNext > TimestampThreshold && headTime >= remoteNext);
        }

        private readonly struct ForkBoundary
        {
            public ulong Value { get; }
            public bool IsBlock { get; }
            public ForkBoundary(ulong value, bool isBlock) { Value = value; IsBlock = isBlock; }
        }

        private static ForkBoundary[] OrderedBoundaries(ulong[] forkBlocks, ulong[] forkTimestamps)
        {
            var blocks = SortDedupNonZero(forkBlocks).Select(v => new ForkBoundary(v, isBlock: true));
            var timestamps = SortDedupNonZero(forkTimestamps).Select(v => new ForkBoundary(v, isBlock: false));
            return blocks.Concat(timestamps).ToArray();
        }

        private static uint[] ChecksumPrefixSums(byte[] genesisHash, ForkBoundary[] boundaries)
        {
            var sums = new uint[boundaries.Length + 1];
            sums[0] = ComputeForkHash(genesisHash, new ulong[0], new ulong[0]);
            for (var k = 1; k <= boundaries.Length; k++)
            {
                var prefix = boundaries.Take(k).ToArray();
                var blocks = prefix.Where(b => b.IsBlock).Select(b => b.Value).ToArray();
                var timestamps = prefix.Where(b => !b.IsBlock).Select(b => b.Value).ToArray();
                sums[k] = ComputeForkHash(genesisHash, blocks, timestamps);
            }
            return sums;
        }

        private static int FirstUnpassedBoundaryIndex(ForkBoundary[] boundaries, ulong headBlock, ulong headTime)
        {
            for (var i = 0; i < boundaries.Length; i++)
            {
                var passed = boundaries[i].IsBlock ? headBlock >= boundaries[i].Value : headTime >= boundaries[i].Value;
                if (!passed) return i;
            }
            return boundaries.Length;
        }

        private static ulong[] SortDedupNonZero(ulong[] values)
        {
            var last = 0UL;
            var result = new System.Collections.Generic.List<ulong>();
            foreach (var v in (values ?? new ulong[0]).OrderBy(v => v))
            {
                if (v == 0 || v == last) continue;
                result.Add(v);
                last = v;
            }
            return result.ToArray();
        }

        private static readonly uint[] _crcTable = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        private static uint Crc32IeeeUpdate(uint crc, byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                crc = _crcTable[(crc ^ data[i]) & 0xff] ^ (crc >> 8);
            return crc;
        }

        private static byte[] Uint64Be(ulong v)
        {
            var b = new byte[8];
            for (int i = 7; i >= 0; i--) { b[i] = (byte)(v & 0xff); v >>= 8; }
            return b;
        }
    }
}
