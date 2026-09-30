using System;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Freezer.FilterMaps;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public sealed class FilterMapsLogResolver
    {
        private readonly IFilterMapsStore _store;
        private readonly IChainView _chain;
        private readonly FilterMapsParams _p;

        public FilterMapsLogResolver(IFilterMapsStore store, IChainView chain, FilterMapsParams p)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _p = p;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FilterMapsLogResolver.GetLogByLvIndex — resolve a candidate lv index to its log")]
        public ResolvedLog GetLogByLvIndex(long lvIndex)
        {
            var range = _store.ReadRange();
            if (range == null)
                return null;

            var blockNumber = FindOwningBlock(lvIndex, range);
            return blockNumber.HasValue ? ResolveWithinBlock(blockNumber.Value, lvIndex) : null;
        }

        private long? FindOwningBlock(long lvIndex, FilterMapsRange range)
        {
            var lo = range.BlocksFirst;
            var hi = range.BlocksAfterLast - 1;
            if (hi < lo)
                return null;

            long? found = null;
            while (lo <= hi)
            {
                var mid = lo + (hi - lo) / 2;
                var start = _store.ReadBlockLvPointer(mid);
                if (!start.HasValue)
                    throw new FreezerConsistencyException($"filter maps query: missing block-lv pointer for indexed block {mid}");

                if (start.Value <= lvIndex)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found;
        }

        private ResolvedLog ResolveWithinBlock(long blockNumber, long lvIndex)
        {
            var current = _store.ReadBlockLvPointer(blockNumber)
                ?? throw new FreezerConsistencyException($"filter maps query: missing block-lv pointer for indexed block {blockNumber}");

            var receipts = _chain.Receipts(blockNumber);
            var logIndex = 0;

            for (var txIndex = 0; txIndex < receipts.Count; txIndex++)
            {
                var logs = receipts[txIndex].Logs;
                for (var l = 0; l < logs.Count; l++)
                {
                    var log = logs[l];
                    var groupSize = 1 + log.Topics.Count;
                    var remaining = _p.ValuesPerMap - (int)(current % _p.ValuesPerMap);
                    if (groupSize > remaining)
                        current += remaining;

                    if (current > lvIndex)
                        return null;

                    if (current == lvIndex)
                        return new ResolvedLog(blockNumber, txIndex, logIndex, log);
                    current++;

                    for (var t = 0; t < log.Topics.Count; t++)
                    {
                        if (current == lvIndex)
                            return null;
                        current++;
                    }

                    logIndex++;
                }
            }

            return null;
        }
    }
}
