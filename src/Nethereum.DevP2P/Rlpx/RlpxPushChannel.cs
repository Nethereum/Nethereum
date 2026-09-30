using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Rlpx
{
    internal sealed class RlpxPushChannel : IDisposable
    {
        private const int Capacity = 1024;

        private readonly Action<RlpxConnection.RlpxPushMessageEventArgs> _deliver;
        private Channel<RlpxConnection.RlpxPushMessageEventArgs> _channel;
        private Task _pumpTask;
        private CancellationTokenSource _pumpCts;
        private bool _disposed;
        private readonly object _lock = new object();

        public RlpxPushChannel(Action<RlpxConnection.RlpxPushMessageEventArgs> deliver)
        {
            _deliver = deliver;
        }

        public void Enqueue(RlpxConnection.RlpxPushMessageEventArgs args)
        {
            EnsurePumpStarted();
            var channel = _channel;
            if (channel == null) return;
            channel.Writer.TryWrite(args);
        }

        private void EnsurePumpStarted()
        {
            if (_pumpTask != null) return;
            lock (_lock)
            {
                if (_pumpTask != null || _disposed) return;
                _channel = Channel.CreateBounded<RlpxConnection.RlpxPushMessageEventArgs>(
                    new BoundedChannelOptions(Capacity)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleReader = true,
                        SingleWriter = true
                    });
                _pumpCts = new CancellationTokenSource();
                _pumpTask = Task.Run(() => PumpAsync(_channel.Reader, _pumpCts.Token));
            }
        }

        private async Task PumpAsync(ChannelReader<RlpxConnection.RlpxPushMessageEventArgs> reader, CancellationToken ct)
        {
            try
            {
                while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var args))
                    {
                        try { _deliver(args); }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                try { _pumpCts?.Cancel(); } catch { }
                try { _channel?.Writer.TryComplete(); } catch { }
                try { _pumpCts?.Dispose(); } catch { }
            }
        }
    }
}
