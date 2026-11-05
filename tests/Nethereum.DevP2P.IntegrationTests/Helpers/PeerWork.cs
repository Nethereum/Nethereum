using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public sealed class PeerWork
    {
        private readonly ConcurrentQueue<Task> _tasks = new ConcurrentQueue<Task>();
        private readonly ConcurrentQueue<Exception> _faults = new ConcurrentQueue<Exception>();
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();

        public EventHandler<T> OnEvent<T>(Func<T, Task> work) =>
            (_, argument) => _tasks.Enqueue(RunAsync(work, argument));

        public void Log(string line) => _lines.Enqueue(line);

        public async Task DrainAsync(ITestOutputHelper output, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            var settled = 0;
            while (true)
            {
                var snapshot = _tasks.ToArray();
                if (snapshot.Length == settled) break;
                var all = Task.WhenAll(snapshot);
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || await Task.WhenAny(all, Task.Delay(remaining)) != all)
                {
                    Flush(output);
                    throw new TimeoutException(
                        $"peer work did not finish within {timeout.TotalSeconds:0.#}s: {snapshot.Count(t => !t.IsCompleted)} of {snapshot.Length} handlers still running");
                }
                settled = snapshot.Length;
            }

            Flush(output);
            if (!_faults.IsEmpty) throw new AggregateException("peer work faulted", _faults.ToArray());
        }

        private void Flush(ITestOutputHelper output)
        {
            while (_lines.TryDequeue(out var line)) output.WriteLine(line);
        }

        private async Task RunAsync<T>(Func<T, Task> work, T argument)
        {
            try
            {
                await work(argument).ConfigureAwait(false);
            }
            catch (Exception ex) when (!IsTransportTeardown(ex))
            {
                _faults.Enqueue(ex);
            }
        }

        private static bool IsTransportTeardown(Exception ex) =>
            ex is OperationCanceledException || ex is ObjectDisposedException
            || ex is IOException || ex is SocketException;
    }
}
