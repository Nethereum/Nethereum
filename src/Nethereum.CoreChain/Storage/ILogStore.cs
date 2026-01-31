using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface ILogStore
    {
        Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex);
        Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom);
        Task<List<FilteredLog>> GetLogsAsync(LogFilter filter);
        Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash);
        Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash);
        Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber);
    }
}
