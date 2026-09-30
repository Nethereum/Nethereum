using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Freezer;
using Nethereum.Freezer.FilterMaps;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public static class LogValueHashEmitter
    {
        public static long HashCollectedValues(
            Dictionary<long, List<(long LvIndex, byte[] ValueHash)>> readyMaps,
            int degreeOfParallelism)
        {
            if (readyMaps == null) throw new ArgumentNullException(nameof(readyMaps));

            var work = new List<(List<(long LvIndex, byte[] ValueHash)> Bucket, int Index)>();
            foreach (var bucket in readyMaps.Values)
                for (var i = 0; i < bucket.Count; i++)
                    work.Add((bucket, i));

            if (work.Count == 0)
                return 0;

            var options = new ParallelOptions { MaxDegreeOfParallelism = FrozenParallelism.Resolve(degreeOfParallelism) };
            Parallel.For(0, work.Count, options, k =>
            {
                var (bucket, index) = work[k];
                var raw = bucket[index].ValueHash;
                var hashed = raw.Length == 20 ? LogValueHasher.AddressValue(raw) : LogValueHasher.TopicValue(raw);
                bucket[index] = (bucket[index].LvIndex, hashed);
            });

            return work.Count;
        }
    }
}
