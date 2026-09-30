using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class CompositeLogStoreTests
    {
        private sealed class RecordingLogStore : ILogStore
        {
            public readonly List<string> Calls = new List<string>();
            public List<FilteredLog> LogsToReturn = new List<FilteredLog>();

            public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
            {
                Calls.Add(nameof(GetLogsAsync));
                return Task.FromResult(LogsToReturn);
            }

            public Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash)
            {
                Calls.Add(nameof(GetLogsByTxHashAsync));
                return Task.FromResult(LogsToReturn);
            }

            public Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash)
            {
                Calls.Add(nameof(GetLogsByBlockHashAsync));
                return Task.FromResult(LogsToReturn);
            }

            public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
            {
                Calls.Add(nameof(GetLogsByBlockNumberAsync));
                return Task.FromResult(LogsToReturn);
            }

            public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
            {
                Calls.Add(nameof(SaveLogsAsync));
                return Task.CompletedTask;
            }

            public Task SaveManyLogsAsync(IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> txLogs, byte[] blockHash, BigInteger blockNumber)
            {
                Calls.Add(nameof(SaveManyLogsAsync));
                return Task.CompletedTask;
            }

            public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom)
            {
                Calls.Add(nameof(SaveBlockBloomAsync));
                return Task.CompletedTask;
            }

            public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
            {
                Calls.Add(nameof(DeleteByBlockNumberAsync));
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void Ctor_NullL1_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CompositeLogStore(null));
        }

        [Fact]
        public async Task Reads_AlwaysGoToL1_NeverL2()
        {
            var l1 = new RecordingLogStore { LogsToReturn = new List<FilteredLog> { new FilteredLog { Address = "0xabc" } } };
            var l2 = new RecordingLogStore();
            var composite = new CompositeLogStore(l1, l2);

            var byFilter = await composite.GetLogsAsync(new LogFilter());
            var byTx = await composite.GetLogsByTxHashAsync(new byte[32]);
            var byBlockHash = await composite.GetLogsByBlockHashAsync(new byte[32]);
            var byBlockNumber = await composite.GetLogsByBlockNumberAsync(1);

            Assert.Same(l1.LogsToReturn, byFilter);
            Assert.Same(l1.LogsToReturn, byTx);
            Assert.Same(l1.LogsToReturn, byBlockHash);
            Assert.Same(l1.LogsToReturn, byBlockNumber);

            Assert.Equal(new[] { "GetLogsAsync", "GetLogsByTxHashAsync", "GetLogsByBlockHashAsync", "GetLogsByBlockNumberAsync" }, l1.Calls);
            Assert.Empty(l2.Calls);
        }

        [Fact]
        public async Task Reads_WorkWithNoL2Wired()
        {
            var l1 = new RecordingLogStore { LogsToReturn = new List<FilteredLog>() };
            var composite = new CompositeLogStore(l1);

            var result = await composite.GetLogsAsync(new LogFilter());

            Assert.Same(l1.LogsToReturn, result);
            Assert.Equal(new[] { "GetLogsAsync" }, l1.Calls);
        }

        [Fact]
        public async Task Writes_ForwardToL2_WhenWired_NeverToL1()
        {
            var l1 = new RecordingLogStore();
            var l2 = new RecordingLogStore();
            var composite = new CompositeLogStore(l1, l2);

            await composite.SaveLogsAsync(new List<Log>(), new byte[32], new byte[32], 1, 0);
            await composite.SaveManyLogsAsync(new List<(List<Log>, byte[], int)>(), new byte[32], 1);
            await composite.SaveBlockBloomAsync(1, new byte[256]);
            await composite.DeleteByBlockNumberAsync(1);

            Assert.Equal(
                new[] { "SaveLogsAsync", "SaveManyLogsAsync", "SaveBlockBloomAsync", "DeleteByBlockNumberAsync" },
                l2.Calls);
            Assert.Empty(l1.Calls);
        }

        [Fact]
        public async Task Writes_AreNoOps_WhenNoL2Wired()
        {
            var l1 = new RecordingLogStore();
            var composite = new CompositeLogStore(l1);

            await composite.SaveLogsAsync(new List<Log>(), new byte[32], new byte[32], 1, 0);
            await composite.SaveManyLogsAsync(new List<(List<Log>, byte[], int)>(), new byte[32], 1);
            await composite.SaveBlockBloomAsync(1, new byte[256]);
            await composite.DeleteByBlockNumberAsync(1);

            Assert.Empty(l1.Calls);
        }
    }
}
