using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerAwareBloomScan : IHistoricalLogScan
    {
        private readonly IReceiptStore _receipts;
        private readonly IBlockStore _blocks;

        public FreezerAwareBloomScan(IReceiptStore receipts, IBlockStore blocks)
        {
            _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        }

        public async Task<IReadOnlyList<ResolvedLog>> ScanAsync(LogFilter filter, long fromBlock, long toBlock)
        {
            var results = new List<ResolvedLog>();
            if (filter == null || toBlock < fromBlock) return results;

            var queryTerms = RocksDbLogStore.BuildQueryBloomTerms(filter);

            for (var blockNumber = fromBlock; blockNumber <= toBlock; blockNumber++)
            {
                if (queryTerms != null)
                {
                    var header = await _blocks.GetByNumberAsync(blockNumber).ConfigureAwait(false);
                    if (header == null) continue;
                    if (!RocksDbLogStore.MatchesQueryBloomTerms(queryTerms, header.LogsBloom))
                        continue;
                }

                var receipts = await _receipts.GetByBlockNumberAsync(blockNumber).ConfigureAwait(false);
                if (receipts == null || receipts.Count == 0) continue;

                var logIndex = 0;
                for (var txIndex = 0; txIndex < receipts.Count; txIndex++)
                {
                    var logs = receipts[txIndex]?.Logs;
                    if (logs == null) continue;
                    for (var l = 0; l < logs.Count; l++)
                    {
                        var log = logs[l];
                        if (filter.MatchesAddress(log.Address) && filter.MatchesTopics(log.Topics))
                            results.Add(new ResolvedLog(blockNumber, txIndex, logIndex, log));
                        logIndex++;
                    }
                }
            }

            return results;
        }
    }
}
