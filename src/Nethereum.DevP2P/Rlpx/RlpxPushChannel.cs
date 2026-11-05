using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Rlpx
{
    internal sealed class RlpxPushChannel : IDisposable
    {
        public const int MaxQueuedMessages = 1024;

        public const long MaxQueuedBytes = RlpxFrameReader.MaxFrameSize + 1L;

        private readonly Action<RlpxConnection.RlpxPushMessageEventArgs> _deliver;
        private Channel<RlpxConnection.RlpxPushMessageEventArgs> _channel;
        private Task _pumpTask;
        private CancellationTokenSource _pumpCts;
        private TaskCompletionSource<bool> _spaceFreed;
        private long _queuedBytes;
        private int _queuedCount;
        private bool _disposed;
        private readonly object _lock = new object();

        private readonly InFlightDecodeLimiter _decodeLimiter;

        public RlpxPushChannel(Action<RlpxConnection.RlpxPushMessageEventArgs> deliver, InFlightDecodeLimiter decodeLimiter)
        {
            _deliver = deliver;
            _decodeLimiter = decodeLimiter;
        }

        public async Task EnqueueAsync(RlpxConnection.RlpxPushMessageEventArgs args, CancellationToken ct)
        {
            EnsurePumpStarted();
            var size = args.Payload?.Length ?? 0;
            while (true)
            {
                Task wait;
                lock (_lock)
                {
                    if (_disposed) return;
                    if (_queuedCount == 0
                        || (_queuedCount < MaxQueuedMessages && _queuedBytes + size <= MaxQueuedBytes))
                    {
                        _queuedBytes += size;
                        _queuedCount++;
                        _channel.Writer.TryWrite(args);
                        return;
                    }
                    wait = (_spaceFreed ??= new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously)).Task;
                }
                await wait.WaitAsync(ct).ConfigureAwait(false);
            }
        }

        private void EnsurePumpStarted()
        {
            if (_pumpTask != null) return;
            lock (_lock)
            {
                if (_pumpTask != null || _disposed) return;
                _channel = Channel.CreateUnbounded<RlpxConnection.RlpxPushMessageEventArgs>(
                    new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
                _pumpCts = new CancellationTokenSource();
                _pumpTask = Task.Run(() => PumpAsync(_channel.Reader, _pumpCts.Token));
            }
        }

        private void Release(int size)
        {
            TaskCompletionSource<bool> waiter;
            lock (_lock)
            {
                _queuedBytes -= size;
                _queuedCount--;
                waiter = _spaceFreed;
                _spaceFreed = null;
            }
            waiter?.TrySetResult(true);
        }

        private async Task PumpAsync(ChannelReader<RlpxConnection.RlpxPushMessageEventArgs> reader, CancellationToken ct)
        {
            try
            {
                while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var args))
                    {
                        var size = args.Payload?.Length ?? 0;
                        InFlightDecodeLease lease = null;
                        try
                        {
                            lease = await _decodeLimiter.AcquireAsync(size, ct).ConfigureAwait(false);
                            _deliver(args);
                        }
                        catch { }
                        finally { lease?.Dispose(); }
                        Release(size);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        public void Dispose()
        {
            TaskCompletionSource<bool> waiter;
            lock (_lock)
            {
                _disposed = true;
                waiter = _spaceFreed;
                _spaceFreed = null;
                try { _pumpCts?.Cancel(); } catch { }
                try { _channel?.Writer.TryComplete(); } catch { }
                try { _pumpCts?.Dispose(); } catch { }
            }
            waiter?.TrySetResult(true);
        }
    }
}
