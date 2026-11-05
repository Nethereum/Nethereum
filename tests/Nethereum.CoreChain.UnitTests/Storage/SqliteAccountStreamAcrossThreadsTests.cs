using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class SqliteAccountStreamAcrossThreadsTests : IDisposable
    {
        private static readonly byte[] Value = { 0x01, 0x02 };

        private readonly SqliteStorageManager _manager;
        private readonly SqliteStateStore _store;

        public SqliteAccountStreamAcrossThreadsTests()
        {
            _manager = new SqliteStorageManager(null, deleteOnDispose: true);
            _store = new SqliteStateStore(_manager);
        }

        public void Dispose()
        {
            Task.Run(_manager.Dispose).Wait(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task Given_AnAccountStream_When_TheConsumerAwaitsOnAnotherThreadBetweenItems_Then_OtherReadersProceedAndTheStreamDisposesCleanly()
        {
            for (var i = 1; i <= 5; i++)
                await _store.SaveAccountAsync("0x" + i.ToString("x40"), new Account { Balance = i });
            await _store.SaveStorageAsync("0x" + 1.ToString("x40"), BigInteger.One, Value);

            var streamed = new List<string>();
            var consumer = Task.Run(async () =>
            {
                await foreach (var kv in _store.StreamAccountsAsync())
                {
                    streamed.Add(kv.Key);
                    await Task.Yield();
                    await Task.Run(() => { });

                    var concurrent = Task.Run(() => _store.GetStorageAsync("0x" + 1.ToString("x40"), BigInteger.One));
                    var finished = await Task.WhenAny(concurrent, Task.Delay(TimeSpan.FromSeconds(5)));
                    Assert.Same(concurrent, finished);
                    Assert.Equal(Value, await concurrent);
                }
            });

            var completed = await Task.WhenAny(consumer, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.Same(consumer, completed);
            await consumer;
            Assert.Equal(5, streamed.Count);
        }
    }
}
