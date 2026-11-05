using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class WorkQueue : IDisposable
    {
        private readonly CancellationTokenSource _stop;
        private readonly System.Collections.Concurrent.BlockingCollection<Action> _items = new(new System.Collections.Concurrent.ConcurrentStack<Action>());
        private int _outstanding;

        public WorkQueue(CancellationToken ct)
        {
            _stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }

        public CancellationToken Token => _stop.Token;

        public void Enqueue(Action work)
        {
            Interlocked.Increment(ref _outstanding);
            _items.Add(work);
        }

        public async Task DrainAsync(int workerCount)
        {
            if (Volatile.Read(ref _outstanding) == 0) return;
            var workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++) workers[i] = Task.Run(Drain, CancellationToken.None);
            try
            {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                foreach (var worker in workers)
                    if (worker.Exception?.InnerException is { } failure && failure is not OperationCanceledException)
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                throw;
            }
        }

        private void Drain()
        {
            try
            {
                foreach (var work in _items.GetConsumingEnumerable(_stop.Token))
                {
                    work();
                    if (Interlocked.Decrement(ref _outstanding) == 0) _items.CompleteAdding();
                }
            }
            catch
            {
                _stop.Cancel();
                throw;
            }
        }

        public void Dispose()
        {
            _items.Dispose();
            _stop.Dispose();
        }
    }
}
