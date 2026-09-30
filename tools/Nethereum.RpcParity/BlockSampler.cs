using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;

namespace Nethereum.RpcParity
{
    /// <summary>
    /// Picks a spread of block numbers to sample across [from, to]: a few
    /// blocks near the tip plus ~8 evenly spaced blocks across the range, so
    /// both the hot window and deeper history get exercised in one run.
    /// </summary>
    public static class BlockSampler
    {
        private const int SpreadCount = 8;
        private const int TailCount = 3;
        private const long DefaultRangeSpan = 10_000;

        public static async Task<List<long>> ResolveAsync(Web3.Web3 nodeX, Web3.Web3 nodeY, CliOptions options)
        {
            if (options.ExplicitBlocks.Count > 0)
                return options.ExplicitBlocks.Distinct().OrderBy(n => n).ToList();

            var to = options.To ?? await LatestCommonBlockAsync(nodeX, nodeY).ConfigureAwait(false);
            var from = options.From ?? Math.Max(0, to - DefaultRangeSpan);

            var samples = new SortedSet<long>();
            for (int i = 0; i < TailCount; i++)
            {
                var n = to - i;
                if (n >= from) samples.Add(n);
            }

            if (to > from)
            {
                for (int i = 0; i < SpreadCount; i++)
                    samples.Add(from + (to - from) * i / (SpreadCount - 1));
            }
            else
            {
                samples.Add(from);
            }

            return samples.ToList();
        }

        // The tip on a subject follower can lag the reference node, so the
        // sample window never asks for blocks node X does not have yet.
        private static async Task<long> LatestCommonBlockAsync(Web3.Web3 nodeX, Web3.Web3 nodeY)
        {
            var latestX = await nodeX.Eth.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
            var latestY = await nodeY.Eth.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
            return (long)BigInteger.Min(latestX.Value, latestY.Value);
        }
    }
}
