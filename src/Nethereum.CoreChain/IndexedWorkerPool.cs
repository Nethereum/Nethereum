using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Nethereum.CoreChain
{
    internal sealed class IndexedWorkerPool : IDisposable
    {
        public const string WorkerThreadNamePrefix = "state-root-";

        private static readonly TimeSpan ShutdownBound = TimeSpan.FromSeconds(5);

        private readonly object _gate = new object();
        private readonly int _workers;
        private DedicatedThreads? _threads;
        private Action<int>? _work;
        private int _count;
        private int _next;
        private int _running;
        private ExceptionDispatchInfo? _failure;
        private bool _disposed;

        public IndexedWorkerPool(int workers)
        {
            if (workers < 1) throw new ArgumentOutOfRangeException(nameof(workers), "A worker pool needs at least one worker.");
            _workers = workers;
        }

        public int ThreadsStarted => _threads?.Count ?? 0;

        public void Run(int count, Action<int> work)
        {
            if (count == 0) return;
            if (_workers == 1 || count == 1)
            {
                for (var i = 0; i < count; i++) work(i);
                return;
            }

            StartThreadsOnce();
            ExceptionDispatchInfo? failure;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(IndexedWorkerPool));
                if (_work != null) throw new InvalidOperationException("A batch is already running on this pool.");
                _work = work;
                _count = count;
                _next = 0;
                _failure = null;
                Monitor.PulseAll(_gate);
                while (!_disposed && (_next < _count || _running > 0)) Monitor.Wait(_gate);
                failure = _failure;
                _failure = null;
                _work = null;
                if (_disposed) throw new ObjectDisposedException(nameof(IndexedWorkerPool));
            }
            failure?.Throw();
        }

        private void StartThreadsOnce()
        {
            lock (_gate)
            {
                if (_disposed || _threads != null) return;
                _threads = new DedicatedThreads(WorkerThreadNamePrefix, _workers, RunWorker);
            }
        }

        private void RunWorker()
        {
            while (true)
            {
                Action<int> work;
                int index;
                lock (_gate)
                {
                    while (!_disposed && (_work == null || _next >= _count)) Monitor.Wait(_gate);
                    if (_disposed) return;
                    work = _work!;
                    index = _next++;
                    _running++;
                }
                try
                {
                    work(index);
                }
                catch (Exception ex)
                {
                    lock (_gate)
                    {
                        _failure ??= ExceptionDispatchInfo.Capture(ex);
                        _next = _count;
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        _running--;
                        if (_next >= _count && _running == 0) Monitor.PulseAll(_gate);
                    }
                }
            }
        }

        public void Dispose()
        {
            DedicatedThreads? threads;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                threads = _threads;
                Monitor.PulseAll(_gate);
            }
            threads?.JoinWithin(ShutdownBound);
        }
    }
}
