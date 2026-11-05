using System;

namespace Nethereum.ABI.Decoders
{
    internal static class AbiOffsetTable
    {
        internal static int[] ComputeEnds(int[] offsets, int length)
        {
            var count = offsets.Length;
            var ends = new int[count];
            if (count == 0) return ends;

            var ascending = true;
            for (var i = 1; i < count; i++)
            {
                if (offsets[i] == offsets[i - 1]) throw DuplicateOffset(offsets[i]);
                if (offsets[i] < offsets[i - 1]) ascending = false;
            }

            if (ascending)
            {
                for (var i = 0; i < count - 1; i++) ends[i] = offsets[i + 1];
                ends[count - 1] = length;
                return ends;
            }

            var sorted = (int[]) offsets.Clone();
            Array.Sort(sorted);
            for (var i = 1; i < count; i++)
                if (sorted[i] == sorted[i - 1]) throw DuplicateOffset(sorted[i]);

            for (var i = 0; i < count; i++)
            {
                var index = Array.BinarySearch(sorted, offsets[i]);
                ends[i] = index + 1 < count ? sorted[index + 1] : length;
            }

            return ends;
        }

        internal static byte[] Slice(byte[] source, long start, long count)
        {
            var available = source.Length - start;
            if (count > available) count = available;
            if (count <= 0) return new byte[0];
            var result = new byte[count];
            Array.Copy(source, start, result, 0, count);
            return result;
        }

        private static Exception DuplicateOffset(int offset)
        {
            return new Exception($@"Duplicate offset in encoded data. Offset: {offset}");
        }
    }
}
