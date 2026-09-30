using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FreezerFilterMapsLogStore : ILogStore
    {
        private readonly FilterMapsQueryEngine _engine;
        private readonly IBlockStore _blocks;
        private readonly ITransactionStore _transactions;

        public FreezerFilterMapsLogStore(FilterMapsQueryEngine engine, IBlockStore blocks, ITransactionStore transactions)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        }

        public async Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            if (filter == null) return new List<FilteredLog>();

            if (filter.FromBlock == null) filter.FromBlock = 0;
            if (filter.ToBlock == null) filter.ToBlock = await _blocks.GetHeightAsync().ConfigureAwait(false);

            return await ResolveAndMapAsync(filter).ConfigureAwait(false);
        }

        public async Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber < BigInteger.Zero) return new List<FilteredLog>();
            var filter = new LogFilter { FromBlock = blockNumber, ToBlock = blockNumber };
            return await ResolveAndMapAsync(filter).ConfigureAwait(false);
        }

        public async Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return new List<FilteredLog>();
            var header = await _blocks.GetByHashAsync(blockHash).ConfigureAwait(false);
            if (header == null) return new List<FilteredLog>();

            var number = header.BlockNumber.ToBigInteger();
            var filter = new LogFilter { FromBlock = number, ToBlock = number };
            return await ResolveAndMapAsync(filter).ConfigureAwait(false);
        }

        public async Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash)
        {
            if (txHash == null) return new List<FilteredLog>();
            var location = await _transactions.GetLocationAsync(txHash).ConfigureAwait(false);
            if (location == null) return new List<FilteredLog>();

            var filter = new LogFilter { FromBlock = location.BlockNumber, ToBlock = location.BlockNumber };
            var blockLogs = await ResolveAndMapAsync(filter).ConfigureAwait(false);

            var result = blockLogs.Where(l => l.TransactionIndex == location.TransactionIndex).ToList();
            result.Sort((a, b) => a.LogIndex.CompareTo(b.LogIndex));
            return result;
        }

        public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
            => Task.CompletedTask;

        public Task SaveManyLogsAsync(
            IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> txLogs, byte[] blockHash, BigInteger blockNumber)
            => Task.CompletedTask;

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom) => Task.CompletedTask;

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber) => Task.CompletedTask;


        private async Task<List<FilteredLog>> ResolveAndMapAsync(LogFilter filter)
        {
            var resolved = await _engine.GetLogsAsync(filter).ConfigureAwait(false);
            var result = new List<FilteredLog>(resolved.Count);

            var blockHashCache = new Dictionary<long, byte[]>();
            var txHashCache = new Dictionary<long, IReadOnlyList<byte[]>>();

            foreach (var r in resolved)
            {
                if (!blockHashCache.TryGetValue(r.BlockNumber, out var blockHash))
                {
                    blockHash = await _blocks.GetHashByNumberAsync(r.BlockNumber).ConfigureAwait(false);
                    blockHashCache[r.BlockNumber] = blockHash;
                }

                if (!txHashCache.TryGetValue(r.BlockNumber, out var txHashes))
                {
                    var txs = await _transactions.GetByBlockNumberAsync(r.BlockNumber).ConfigureAwait(false);
                    txHashes = txs?.Select(t => t.Hash).ToList() ?? new List<byte[]>();
                    txHashCache[r.BlockNumber] = txHashes;
                }

                var txHash = r.TransactionIndex >= 0 && r.TransactionIndex < txHashes.Count
                    ? txHashes[r.TransactionIndex]
                    : null;

                result.Add(FilteredLog.FromLog(r.Log, blockHash, r.BlockNumber, txHash, r.TransactionIndex, r.LogIndex));
            }

            return result;
        }
    }
}
