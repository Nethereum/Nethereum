using System;
using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class SqliteSavepointScopeAcrossThreadsTests : IDisposable
    {
        private const string Address = "0x1234";
        private static readonly byte[] Committed = { 0x01, 0x02 };
        private static readonly byte[] Uncommitted = { 0xFF, 0xFE };

        private readonly SqliteStorageManager _manager;
        private readonly SqliteStateStore _store;
        private readonly ManualResetEventSlim _threadsAlive = new ManualResetEventSlim(false);

        public SqliteSavepointScopeAcrossThreadsTests()
        {
            _manager = new SqliteStorageManager(null, deleteOnDispose: true);
            _store = new SqliteStateStore(_manager);
        }

        public void Dispose()
        {
            _threadsAlive.Set();
            _manager.Dispose();
        }

        [Fact]
        public async Task Given_AnOpenSavepoint_When_AParallelReaderOnAnotherThreadReadsTheUncommittedWrite_Then_ItSeesTheWrite()
        {
            await _store.SaveAccountAsync(Address, new Account { Balance = 100 });
            await _store.SaveStorageAsync(Address, BigInteger.One, Committed);
            var snapshot = await _store.CreateSnapshotAsync();
            await _store.SaveStorageAsync(Address, BigInteger.One, Uncommitted);

            var seen = new ConcurrentBag<byte[]>();
            var threads = new ConcurrentBag<int>();
            using var allStarted = new System.Threading.Barrier(8);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(() =>
            {
                allStarted.SignalAndWait();
                threads.Add(Environment.CurrentManagedThreadId);
                for (var i = 0; i < 4; i++)
                    seen.Add(_store.GetStorageAsync(Address, BigInteger.One).GetAwaiter().GetResult());
            }, TaskCreationOptions.LongRunning)));

            var viaParallelLoop = new ConcurrentBag<byte[]>();
            await Parallel.ForEachAsync(Enumerable.Range(0, 32), async (_, __) =>
            {
                viaParallelLoop.Add(await _store.GetStorageAsync(Address, BigInteger.One));
            });

            Assert.Equal(8, threads.Distinct().Count());
            Assert.Equal(32, seen.Count);
            Assert.All(seen, v => Assert.Equal(Uncommitted, v));
            Assert.All(viaParallelLoop, v => Assert.Equal(Uncommitted, v));

            await _store.RevertSnapshotAsync(snapshot);

            Assert.Equal(Committed, await _store.GetStorageAsync(Address, BigInteger.One));
            var afterRollback = await Task.WhenAll(Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => _store.GetStorageAsync(Address, BigInteger.One))));
            Assert.All(afterRollback, v => Assert.Equal(Committed, v));
        }

        [Fact]
        public async Task Given_AnOpenSavepoint_When_ItAwaitsAndResumesOnAnotherThread_Then_ReleaseAndRollbackFindTheSavepoint()
        {
            await _store.SaveAccountAsync(Address, new Account { Balance = 100 });
            await _store.SaveStorageAsync(Address, BigInteger.One, Committed);

            IStateSnapshot toCommit = null;
            var createdOn = await OnDedicatedThread(() =>
            {
                toCommit = _store.CreateSnapshotAsync().GetAwaiter().GetResult();
                return Environment.CurrentManagedThreadId;
            });

            var committedOn = await OnDedicatedThread(() =>
            {
                _store.SaveStorageAsync(Address, BigInteger.One, Uncommitted).GetAwaiter().GetResult();
                _store.CommitSnapshotAsync(toCommit).GetAwaiter().GetResult();
                return Environment.CurrentManagedThreadId;
            });
            Assert.NotEqual(createdOn, committedOn);
            Assert.Equal(Uncommitted, await _store.GetStorageAsync(Address, BigInteger.One));

            IStateSnapshot toRevert = null;
            var revertCreatedOn = await OnDedicatedThread(() =>
            {
                toRevert = _store.CreateSnapshotAsync().GetAwaiter().GetResult();
                _store.SaveStorageAsync(Address, BigInteger.One, Committed).GetAwaiter().GetResult();
                return Environment.CurrentManagedThreadId;
            });

            var revertedOn = await OnDedicatedThread(() =>
            {
                _store.RevertSnapshotAsync(toRevert).GetAwaiter().GetResult();
                return Environment.CurrentManagedThreadId;
            });

            Assert.NotEqual(revertCreatedOn, revertedOn);
            Assert.Equal(Uncommitted, await _store.GetStorageAsync(Address, BigInteger.One));
        }

        [Fact]
        public void Given_TheObsoleteConnectionProperty_When_Used_Then_ItReturnsTheOpenSharedConnection()
        {
#pragma warning disable CS0618
            var connection = _manager.Connection;
#pragma warning restore CS0618

            Assert.NotNull(connection);
            Assert.Equal(ConnectionState.Open, connection.State);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            Assert.Equal(1L, command.ExecuteScalar());
        }

        private Task<T> OnDedicatedThread<T>(Func<T> work)
        {
            var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    result.SetResult(work());
                }
                catch (Exception ex)
                {
                    result.SetException(ex);
                }
                _threadsAlive.Wait();
            }) { IsBackground = true };
            thread.Start();
            return result.Task;
        }
    }
}
