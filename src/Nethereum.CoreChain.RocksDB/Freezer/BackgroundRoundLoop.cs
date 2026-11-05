using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Freezer
{
    internal sealed class BackgroundRoundLoop
    {
        private const int FirstBackoffMs = 1_000;
        private const int MaxBackoffMs = 30_000;

        private readonly string _name;
        private readonly string _pausingLine;
        private readonly string _resumingLine;
        private readonly Func<bool> _shouldPause;
        private readonly Func<string> _describePause;
        private readonly TimeSpan _pausePollInterval;
        private readonly TimeSpan _roundInterval;
        private readonly object _gate = new object();

        private CancellationTokenSource _cts;
        private Task _task;
        private volatile string _lastError;

        internal BackgroundRoundLoop(
            string name, string pausingLine, string resumingLine,
            Func<bool> shouldPause, Func<string> describePause,
            TimeSpan pausePollInterval, TimeSpan roundInterval)
        {
            _name = name;
            _pausingLine = pausingLine;
            _resumingLine = resumingLine;
            _shouldPause = shouldPause;
            _describePause = describePause;
            _pausePollInterval = pausePollInterval;
            _roundInterval = roundInterval;
        }

        internal bool IsStarted { get { lock (_gate) return _task != null; } }

        internal string LastError => _lastError;

        internal void Start(Action<CancellationToken> round)
        {
            lock (_gate)
            {
                if (_task != null) return;
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _task = Task.Factory.StartNew(
                    () => RunAsync(round, ct), ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            }
        }

        internal async Task StopAsync()
        {
            var (task, cts) = Detach();
            if (task == null) return;
            cts.Cancel();
            try { await task.ConfigureAwait(false); } catch { }
            cts.Dispose();
            Console.Error.WriteLine($"[Nethereum.Freezer] {_name} stopped");
        }

        internal void Stop()
        {
            var (task, cts) = Detach();
            if (task == null) return;
            cts.Cancel();
            try { task.Wait(); } catch { }
            cts.Dispose();
            Console.Error.WriteLine($"[Nethereum.Freezer] {_name} stopped");
        }

        private (Task Task, CancellationTokenSource Cts) Detach()
        {
            lock (_gate)
            {
                var detached = (_task, _cts);
                _task = null;
                _cts = null;
                return detached;
            }
        }

        private async Task RunAsync(Action<CancellationToken> round, CancellationToken ct)
        {
            var backoff = new Backoff(FirstBackoffMs);
            while (!ct.IsCancellationRequested)
            {
                if (!await WaitWhilePausedAsync(ct).ConfigureAwait(false)) break;
                var wait = RunRound(round, backoff, ct);
                if (!await DelayAsync(wait, ct).ConfigureAwait(false)) break;
            }
        }

        private async Task<bool> WaitWhilePausedAsync(CancellationToken ct)
        {
            if (!_shouldPause()) return true;

            Console.Error.WriteLine($"[Nethereum.Freezer] {_pausingLine}: {_describePause()}");
            try
            {
                while (_shouldPause())
                    await Task.Delay(_pausePollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return false; }
            Console.Error.WriteLine($"[Nethereum.Freezer] {_resumingLine}");
            return true;
        }

        private TimeSpan RunRound(Action<CancellationToken> round, Backoff backoff, CancellationToken ct)
        {
            try
            {
                round(ct);
                _lastError = null;
                backoff.Reset();
                return _roundInterval;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                _lastError = $"{ex.GetType().Name}: {ex.Message}";
                Console.Error.WriteLine($"[Nethereum.Freezer] {_name} round failed; backing off: {_lastError}");
                var wait = TimeSpan.FromMilliseconds(backoff.Current);
                backoff.Advance(MaxBackoffMs);
                return wait;
            }
        }

        private static async Task<bool> DelayAsync(TimeSpan wait, CancellationToken ct)
        {
            try
            {
                await Task.Delay(wait, ct).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) { return false; }
        }
    }
}
