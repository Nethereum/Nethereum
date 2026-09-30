using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Documentation;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public sealed class FilterMapsQueryEngine
    {
        private readonly IFilterMapsStore _store;
        private readonly FilterMapsMatcher _matcher;
        private readonly FilterMapsLogResolver _resolver;
        private readonly IHistoricalLogScan _hotScan;

        public FilterMapsQueryEngine(
            IFilterMapsStore store,
            FilterMapsMatcher matcher,
            FilterMapsLogResolver resolver,
            IHistoricalLogScan hotScan)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _hotScan = hotScan ?? throw new ArgumentNullException(nameof(hotScan));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FilterMapsQueryEngine.GetLogsAsync — the eth_getLogs boundary-split query")]
        public async Task<IReadOnlyList<ResolvedLog>> GetLogsAsync(LogFilter filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (filter.ToBlock == null)
                throw new ArgumentException(
                    "GetLogsAsync requires a resolved ToBlock -- tag resolution (\"latest\"/\"pending\") happens upstream, this engine only splits a concrete range",
                    nameof(filter));

            var fromBlock = (long)(filter.FromBlock ?? 0);
            var toBlock = (long)filter.ToBlock.Value;

            var range = _store.ReadRange();
            var indexedHead = range == null ? -1L : range.BlocksAfterLast - 1;

            var results = new List<ResolvedLog>();

            var indexedTo = Math.Min(toBlock, indexedHead);
            if (fromBlock <= indexedTo)
                results.AddRange(QueryFilterMaps(filter, fromBlock, indexedTo));

            var scanFrom = Math.Max(fromBlock, indexedHead + 1);
            if (scanFrom <= toBlock)
                results.AddRange(await _hotScan.ScanAsync(filter, scanFrom, toBlock).ConfigureAwait(false));

            results.Sort(CompareByLocation);
            return results;
        }

        private IEnumerable<ResolvedLog> QueryFilterMaps(LogFilter filter, long fromBlock, long toBlock)
        {
            var query = BuildQuery(filter);
            var candidates = _matcher.GetPotentialMatches(query, fromBlock, toBlock);

            foreach (var lvIndex in candidates)
            {
                var resolved = _resolver.GetLogByLvIndex(lvIndex);
                if (resolved == null)
                    continue;

                if (!filter.MatchesAddress(resolved.Log.Address) || !filter.MatchesTopics(resolved.Log.Topics))
                    continue;

                yield return resolved;
            }
        }

        private static FilterMapsQuery BuildQuery(LogFilter filter)
        {
            var positions = new List<IReadOnlyList<byte[]>> { HashAddresses(filter.Addresses) };

            if (filter.Topics != null)
                foreach (var topicList in filter.Topics)
                    positions.Add(HashTopics(topicList));

            return new FilterMapsQuery(positions);
        }

        private static IReadOnlyList<byte[]> HashAddresses(List<string> addresses)
        {
            var hashed = new List<byte[]>();
            if (addresses != null)
                foreach (var address in addresses)
                    hashed.Add(LogValueHasher.AddressValue(address.HexToByteArray()));
            return hashed;
        }

        private static IReadOnlyList<byte[]> HashTopics(List<byte[]> topics)
        {
            var hashed = new List<byte[]>();
            if (topics != null)
                foreach (var topic in topics)
                    hashed.Add(LogValueHasher.TopicValue(topic));
            return hashed;
        }

        private static int CompareByLocation(ResolvedLog a, ResolvedLog b)
        {
            var byBlock = a.BlockNumber.CompareTo(b.BlockNumber);
            if (byBlock != 0) return byBlock;
            var byTx = a.TransactionIndex.CompareTo(b.TransactionIndex);
            return byTx != 0 ? byTx : a.LogIndex.CompareTo(b.LogIndex);
        }
    }
}
