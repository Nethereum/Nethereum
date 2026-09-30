using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class CompositeLogStore : ILogStore
    {
        private readonly ILogStore _l1;
        private readonly ILogStore _l2;

        public CompositeLogStore(ILogStore l1, ILogStore l2 = null)
        {
            _l1 = l1 ?? throw new System.ArgumentNullException(nameof(l1));
            _l2 = l2;
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter) => _l1.GetLogsAsync(filter);

        public Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash) => _l1.GetLogsByTxHashAsync(txHash);

        public Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash) => _l1.GetLogsByBlockHashAsync(blockHash);

        public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber) => _l1.GetLogsByBlockNumberAsync(blockNumber);

        public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
            => _l2 != null ? _l2.SaveLogsAsync(logs, txHash, blockHash, blockNumber, txIndex) : Task.CompletedTask;

        public Task SaveManyLogsAsync(
            IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> txLogs, byte[] blockHash, BigInteger blockNumber)
            => _l2 != null ? _l2.SaveManyLogsAsync(txLogs, blockHash, blockNumber) : Task.CompletedTask;

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom)
            => _l2 != null ? _l2.SaveBlockBloomAsync(blockNumber, bloom) : Task.CompletedTask;

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            => _l2 != null ? _l2.DeleteByBlockNumberAsync(blockNumber) : Task.CompletedTask;
    }
}
