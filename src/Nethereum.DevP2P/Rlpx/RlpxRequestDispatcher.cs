using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Rlpx
{
    internal enum RlpxControlFrameAction
    {
        Continue,

        PeerDisconnected,

        PassThrough
    }

    internal sealed class RlpxRequestDispatcher : IDisposable
    {
        private sealed class PendingRequest
        {
            public readonly int ExpectedMsgId;
            public readonly RlpxResponseBound Bound;
            public readonly TaskCompletionSource<LeasedPayload> Tcs =
                new TaskCompletionSource<LeasedPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTime? AbandonedExpiry;
            public PendingRequest(int expectedMsgId, RlpxResponseBound bound)
            {
                ExpectedMsgId = expectedMsgId;
                Bound = bound;
            }
        }

        private readonly struct LeasedPayload
        {
            public readonly byte[] Payload;
            public readonly InFlightDecodeLease Lease;
            public LeasedPayload(byte[] payload, InFlightDecodeLease lease)
            {
                Payload = payload;
                Lease = lease;
            }
        }

        private readonly Func<CancellationToken, Task<(int msgId, byte[] payload)>> _readFrame;
        private readonly Func<int, byte[], CancellationToken, Task> _sendFrame;
        private readonly Func<int, CancellationToken, Task<RlpxControlFrameAction>> _handleControlFrame;
        private readonly Func<int, byte[], CancellationToken, Task> _onUnsolicited;
        private readonly Action _onDisconnected;
        private readonly Func<int> _readTimeoutMs;
        private readonly Func<int> _abandonedTrackingMs;
        private readonly Action _onFrameReceived;
        private readonly Func<int, int, bool> _isFrameAccepted;
        private readonly Func<Task> _onProtocolError;
        private readonly Func<int, bool> _isResponseMessage;
        private readonly InFlightDecodeLimiter _decodeLimiter;
        private volatile Exception _terminalFault;

        private readonly ConcurrentDictionary<ulong, PendingRequest> _pendingRequests = new();
        private Task _loopTask;
        private CancellationTokenSource _loopCts;
        private bool _disposed;
        private readonly object _lock = new object();

        public RlpxRequestDispatcher(
            Func<CancellationToken, Task<(int msgId, byte[] payload)>> readFrame,
            Func<int, byte[], CancellationToken, Task> sendFrame,
            Func<int, CancellationToken, Task<RlpxControlFrameAction>> handleControlFrame,
            Func<int, byte[], CancellationToken, Task> onUnsolicited,
            Action onDisconnected,
            Func<int> readTimeoutMs,
            Func<int> abandonedTrackingMs,
            Action onFrameReceived,
            Func<int, int, bool> isFrameAccepted,
            Func<Task> onProtocolError,
            Func<int, bool> isResponseMessage,
            InFlightDecodeLimiter decodeLimiter)
        {
            _readFrame = readFrame;
            _sendFrame = sendFrame;
            _handleControlFrame = handleControlFrame;
            _onUnsolicited = onUnsolicited;
            _onDisconnected = onDisconnected;
            _readTimeoutMs = readTimeoutMs;
            _abandonedTrackingMs = abandonedTrackingMs;
            _onFrameReceived = onFrameReceived;
            _isFrameAccepted = isFrameAccepted;
            _onProtocolError = onProtocolError;
            _isResponseMessage = isResponseMessage;
            _decodeLimiter = decodeLimiter;
        }

        public bool IsLoopStarted => _loopTask != null;

        public Task<byte[]> SendRequestAsync(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, RlpxResponseBound bound, CancellationToken ct = default)
            => SendRequestAsync(sendMsgId, payload, expectedResponseMsgId, requestId, timeout, bound, p => p, ct);

        public async Task<T> SendRequestAsync<T>(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, RlpxResponseBound bound, Func<byte[], T> decode, CancellationToken ct = default)
        {
            EnsureLoopStarted();
            ThrowIfTerminated();
            PurgeExpiredAbandoned();
            var pending = new PendingRequest(expectedResponseMsgId, bound);
            if (!_pendingRequests.TryAdd(requestId, pending))
                throw new InvalidOperationException($"Duplicate in-flight request id {requestId}");
            var sent = false;
            try
            {
                ThrowIfTerminated();
                await _sendFrame(sendMsgId, payload, ct).ConfigureAwait(false);
                sent = true;

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);
                LeasedPayload leased;
                using (timeoutCts.Token.Register(() =>
                {
                    if (ct.IsCancellationRequested) pending.Tcs.TrySetCanceled(ct);
                    else pending.Tcs.TrySetException(new TimeoutException(
                        $"No response (msgId=0x{expectedResponseMsgId:x2}) for request {requestId} within {timeout.TotalSeconds:0}s"));
                }))
                {
                    leased = await pending.Tcs.Task.ConfigureAwait(false);
                }
                try
                {
                    return decode(leased.Payload);
                }
                finally
                {
                    leased.Lease.Dispose();
                }
            }
            finally
            {
                if (sent && !pending.Tcs.Task.IsCompletedSuccessfully && _terminalFault == null)
                {
                    pending.AbandonedExpiry = DateTime.UtcNow.AddMilliseconds(_abandonedTrackingMs());
                }
                else
                {
                    _pendingRequests.TryRemove(requestId, out _);
                }
            }
        }

        private void PurgeExpiredAbandoned()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in _pendingRequests)
            {
                var expiry = entry.Value.AbandonedExpiry;
                if (expiry.HasValue && expiry.Value <= now)
                    _pendingRequests.TryRemove(entry.Key, out _);
            }
        }

        private void ThrowIfTerminated()
        {
            var fault = _terminalFault;
            if (fault == null) return;
            if (fault is ObjectDisposedException) throw new ObjectDisposedException(nameof(RlpxConnection));
            throw new IOException("connection read loop ended", fault);
        }

        internal void EnsureLoopStarted()
        {
            if (_loopTask != null) return;
            lock (_lock)
            {
                if (_loopTask != null || _disposed) return;
                _loopCts = new CancellationTokenSource();
                _loopTask = Task.Run(() => DispatcherLoopAsync(_loopCts.Token));
            }
        }

        private async Task DispatcherLoopAsync(CancellationToken ct)
        {
            try
            {
                Exception fault;
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        int msgId; byte[] payload;
                        using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            readCts.CancelAfter(_readTimeoutMs());
                            (msgId, payload) = await _readFrame(readCts.Token).ConfigureAwait(false);
                        }

                        if (!_isFrameAccepted(msgId, payload.Length))
                        {
                            await _onProtocolError().ConfigureAwait(false);
                            throw new IOException($"message id 0x{msgId:x} with {payload.Length} bytes is outside every negotiated capability or over its size cap");
                        }

                        _onFrameReceived();

                        var action = await _handleControlFrame(msgId, ct).ConfigureAwait(false);
                        if (action == RlpxControlFrameAction.Continue) continue;
                        if (action == RlpxControlFrameAction.PeerDisconnected)
                        {
                            _onDisconnected();
                            throw new IOException("Peer disconnected");
                        }

                        if (_isResponseMessage(msgId))
                            await FulfilResponseAsync(msgId, payload, ct).ConfigureAwait(false);
                        else
                            await _onUnsolicited(msgId, payload, ct).ConfigureAwait(false);
                    }
                    fault = new IOException("connection read loop ended");
                }
                catch (Exception ex)
                {
                    fault = ex;
                }
                if (_terminalFault == null) _terminalFault = fault;
                FaultAllPending(fault);
            }
            finally
            {
                _onDisconnected();
            }
        }

        private async Task FulfilResponseAsync(int msgId, byte[] payload, CancellationToken ct)
        {
            if (!RlpxResponseEnvelope.TryReadRequestId(payload, out var requestId))
                await BreachAsync(null, $"response 0x{msgId:x} carries no request id").ConfigureAwait(false);

            if (!_pendingRequests.TryGetValue(requestId, out var pending)
                || (pending.AbandonedExpiry.HasValue && pending.AbandonedExpiry.Value <= DateTime.UtcNow))
            {
                await BreachAsync(null, $"response 0x{msgId:x} answers no pending request {requestId}").ConfigureAwait(false);
                return;
            }

            if (pending.ExpectedMsgId != msgId)
                await BreachAsync(pending,
                    $"response 0x{msgId:x} answers request {requestId} that expects 0x{pending.ExpectedMsgId:x}").ConfigureAwait(false);

            if (!RlpxResponseEnvelope.TryRead(payload, pending.Bound, out _, out var violation))
                await BreachAsync(pending, $"response 0x{msgId:x} for request {requestId} is malformed").ConfigureAwait(false);
            if (violation != null)
                await BreachAsync(pending, $"response 0x{msgId:x} for request {requestId}: {violation}").ConfigureAwait(false);

            if (pending.AbandonedExpiry.HasValue)
            {
                _pendingRequests.TryRemove(requestId, out _);
                return;
            }

            var lease = await _decodeLimiter.AcquireAsync(payload.Length, ct).ConfigureAwait(false);
            if (!_pendingRequests.TryRemove(requestId, out _)
                || !pending.Tcs.TrySetResult(new LeasedPayload(payload, lease)))
                lease.Dispose();
        }

        private async Task BreachAsync(PendingRequest pending, string reason)
        {
            pending?.Tcs.TrySetException(new InvalidDataException(reason));
            await _onProtocolError().ConfigureAwait(false);
            throw new IOException(reason);
        }

        private void FaultAllPending(Exception ex)
        {
            foreach (var reqId in _pendingRequests.Keys)
                if (_pendingRequests.TryRemove(reqId, out var pending))
                    pending.Tcs.TrySetException(ex);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                var disposedFault = new ObjectDisposedException(nameof(RlpxConnection));
                _terminalFault = disposedFault;
                try { _loopCts?.Cancel(); } catch { }
                FaultAllPending(disposedFault);
                try { _loopCts?.Dispose(); } catch { }
            }
        }
    }
}
