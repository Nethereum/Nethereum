using System.Numerics;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MempoolPendingChains
    {
        public static Dictionary<ChainKey, MempoolEntry[]> BuildContiguousChains(
            IEnumerable<MempoolEntry> eligible)
        {
            var result = new Dictionary<ChainKey, MempoolEntry[]>();

            var groups = eligible.GroupBy(MempoolChainKey.Of);

            foreach (var group in groups)
            {
                var ascending = group.OrderBy(e => e.UserOperation.Nonce).ToArray();

                var run = new List<MempoolEntry> { ascending[0] };
                for (var i = 1; i < ascending.Length; i++)
                {
                    if (ascending[i].UserOperation.Nonce != ascending[i - 1].UserOperation.Nonce + 1)
                    {
                        break;
                    }
                    run.Add(ascending[i]);
                }

                result[group.Key] = run.ToArray();
            }

            return result;
        }

        public static MempoolEntry[] SelectBundleCandidates(
            IReadOnlyCollection<MempoolEntry> eligible, int maxCount, BigInteger? maxGas)
        {
            var chainsByKey = BuildContiguousChains(eligible);

            var candidateOrder = eligible
                .OrderByDescending(e => e.Priority)
                .ThenBy(e => e.SubmittedAt);

            var result = new List<MempoolEntry>();
            var keysDecided = new HashSet<ChainKey>();
            BigInteger totalGas = 0;

            foreach (var entry in candidateOrder)
            {
                if (result.Count >= maxCount) break;

                var chainKey = MempoolChainKey.Of(entry);
                if (keysDecided.Contains(chainKey)) continue;
                keysDecided.Add(chainKey);

                foreach (var queued in chainsByKey[chainKey])
                {
                    if (result.Count >= maxCount) break;

                    if (maxGas.HasValue)
                    {
                        var opGas = queued.UserOperation.GetTotalGas();
                        if (totalGas + opGas > maxGas.Value) break;
                        totalGas += opGas;
                    }

                    result.Add(queued);
                }
            }

            return result.ToArray();
        }
    }
}
