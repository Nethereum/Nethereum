using Nethereum.Documentation;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nethereum.CoreChain.RocksDB
{
    public sealed class RocksDbCacheStats
    {
        public long IndexHit { get; set; }
        public long IndexMiss { get; set; }
        public long FilterHit { get; set; }
        public long FilterMiss { get; set; }
        public long DataHit { get; set; }
        public long DataMiss { get; set; }
        public long BytesRead { get; set; }
        public long UsageBytes { get; set; }

        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "RocksDbCacheStats.Parse - block-cache counters from the RocksDB statistics text")]
        public static RocksDbCacheStats Parse(string statisticsString, string blockCacheUsage)
        {
            var tickers = new Dictionary<string, long>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(statisticsString))
            {
                foreach (var line in statisticsString.Split('\n'))
                {
                    var countAt = line.IndexOf(" COUNT : ", StringComparison.Ordinal);
                    if (countAt <= 0) continue;
                    var name = line.Substring(0, countAt).Trim();
                    var value = line.Substring(countAt + " COUNT : ".Length).Trim();
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                        tickers[name] = count;
                }
            }

            long Ticker(string name) => tickers.TryGetValue(name, out var v) ? v : 0;

            long.TryParse(blockCacheUsage, NumberStyles.Integer, CultureInfo.InvariantCulture, out var usage);

            return new RocksDbCacheStats
            {
                IndexHit = Ticker("rocksdb.block.cache.index.hit"),
                IndexMiss = Ticker("rocksdb.block.cache.index.miss"),
                FilterHit = Ticker("rocksdb.block.cache.filter.hit"),
                FilterMiss = Ticker("rocksdb.block.cache.filter.miss"),
                DataHit = Ticker("rocksdb.block.cache.data.hit"),
                DataMiss = Ticker("rocksdb.block.cache.data.miss"),
                BytesRead = Ticker("rocksdb.block.cache.bytes.read"),
                UsageBytes = usage,
            };
        }
    }
}
