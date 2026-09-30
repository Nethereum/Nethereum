using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Nethereum.Freezer
{
    public static class FreezerParallelSegment
    {
        public static Action<string> StepLog;

        public static IReadOnlyList<(long ItemNumber, T Decoded)> DecodeSealed<T>(
            FreezerTable<T> table, ushort fileNumber, int? maxDegreeOfParallelism = null)
            => DecodeMeasured(table, table.Name, () => table.ReadSealedSegment(fileNumber), maxDegreeOfParallelism);

        public static IReadOnlyList<(long ItemNumber, T Decoded)> DecodeSealedRange<T>(
            FreezerTable<T> table, long startItem, int maxItems, int? maxDegreeOfParallelism = null)
            => DecodeMeasured(table, table.Name, () => table.ReadSealedRange(startItem, maxItems), maxDegreeOfParallelism);

        public static IReadOnlyList<(long ItemNumber, T Decoded)> DecodeSealedRange<T>(
            FreezerTable<T> table, long startItem, int maxItems, long endExclusive, int? maxDegreeOfParallelism = null)
            => DecodeMeasured(table, table.Name, () => table.ReadSealedRange(startItem, maxItems, endExclusive), maxDegreeOfParallelism);

        public static IReadOnlyList<(long ItemNumber, T Decoded)> DecodeSealedChunk<T>(
            FreezerTable<T> table, long startItem, int targetBytes, long endExclusive,
            int? maxDegreeOfParallelism = null)
            => DecodeMeasured(table, table.Name,
                () => table.ReadSealedChunk(startItem, targetBytes, endExclusive).Items,
                maxDegreeOfParallelism);

        private static IReadOnlyList<(long ItemNumber, T Decoded)> DecodeMeasured<T>(
            FreezerTable<T> table, string name, Func<IReadOnlyList<(long, byte[])>> read, int? maxDegreeOfParallelism)
        {
            var log = StepLog;
            if (log == null) return DecodeInParallel(table, read(), maxDegreeOfParallelism);

            var readSw = Stopwatch.StartNew();
            var raw = read();
            readSw.Stop();
            long bytes = 0;
            for (var i = 0; i < raw.Count; i++) bytes += raw[i].Item2.Length;
            var decodeSw = Stopwatch.StartNew();
            var result = DecodeInParallel(table, raw, maxDegreeOfParallelism);
            decodeSw.Stop();
            log($"decode table={name} items={raw.Count} bytes={bytes / (1024.0 * 1024.0):F1}MB " +
                $"read={readSw.ElapsedMilliseconds}ms decode={decodeSw.ElapsedMilliseconds}ms dop={FrozenParallelism.Resolve(maxDegreeOfParallelism)}");
            return result;
        }

        private static IReadOnlyList<(long ItemNumber, T Decoded)> DecodeInParallel<T>(
            FreezerTable<T> table, IReadOnlyList<(long ItemNumber, byte[] Raw)> raw, int? maxDegreeOfParallelism)
        {
            var decoded = new (long ItemNumber, T Decoded)[raw.Count];

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = FrozenParallelism.Resolve(maxDegreeOfParallelism)
            };

            Parallel.For(0, raw.Count, options, i =>
                decoded[i] = (raw[i].ItemNumber, table.Decode(raw[i].Raw)));

            return decoded;
        }
    }
}
