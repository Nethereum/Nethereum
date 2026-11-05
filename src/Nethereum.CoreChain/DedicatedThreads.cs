using System;
using System.Threading;

namespace Nethereum.CoreChain
{
    internal sealed class DedicatedThreads
    {
        private const int WorkerStackBytes = 8 * 1024 * 1024;

        private readonly Thread[] _threads;

        public DedicatedThreads(string namePrefix, int count, ThreadStart body)
        {
            _threads = new Thread[count];
            for (var i = 0; i < count; i++)
            {
                var thread = new Thread(body, WorkerStackBytes)
                {
                    IsBackground = true,
                    Name = namePrefix + i
                };
                _threads[i] = thread;
                thread.Start();
            }
        }

        public int Count => _threads.Length;

        public int Alive
        {
            get
            {
                var alive = 0;
                foreach (var thread in _threads)
                    if (thread.IsAlive) alive++;
                return alive;
            }
        }

        public void JoinWithin(TimeSpan bound)
        {
            var deadline = DateTime.UtcNow + bound;
            foreach (var thread in _threads)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) return;
                thread.Join(remaining);
            }
        }
    }
}
