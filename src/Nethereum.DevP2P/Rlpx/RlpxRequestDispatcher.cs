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
            public readonly TaskCompletionSource<byte[]> Tcs =
                new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            public PendingRequest(int expectedMsgId) => ExpectedMsgId = expectedMsgId;
        }

        private readonly Func<CancellationToken, Task<(int msgId, byte[] payload)>> _readFrame;
        private readonly Func<int, byte[], CancellationToken, Task> _sendFrame;
        private readonly Func<int, CancellationToken, Task<RlpxControlFrameAction>> _handleControlFrame;
        private readonly Action<int, byte[]> _onUnsolicited;
        private readonly Action _onDisconnected;
        private readonly Func<int> _readTimeoutMs;
        private readonly Action _onFrameReceived;

        private readonly ConcurrentDictionary<ulong, PendingRequest> _pendingRequests = new();
        private Task _loopTask;
        private CancellationTokenSource _loopCts;
        private bool _disposed;
        private readonly object _lock = new object();

        public RlpxRequestDispatcher(
            Func<CancellationToken, Task<(int msgId, byte[] payload)>> readFrame,
            Func<int, byte[], CancellationToken, Task> sendFrame,
            Func<int, CancellationToken, Task<RlpxControlFrameAction>> handleControlFrame,
            Action<int, byte[]> onUnsolicited,
            Action onDisconnected,
            Func<int> readTimeoutMs,
            Action onFrameReceived)
        {
            _readFrame = readFrame;
            _sendFrame = sendFrame;
            _handleControlFrame = handleControlFrame;
            _onUnsolicited = onUnsolicited;
            _onDisconnected = onDisconnected;
            _readTimeoutMs = readTimeoutMs;
            _onFrameReceived = onFrameReceived;
        }

        public bool IsLoopStarted => _loopTask != null;

        public async Task<byte[]> SendRequestAsync(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, CancellationToken ct = default)
        {
            EnsureLoopStarted();
            var pending = new PendingRequest(expectedResponseMsgId);
            if (!_pendingRequests.TryAdd(requestId, pending))
                throw new InvalidOperationException($"Duplicate in-flight request id {requestId}");
            try
            {
                await _sendFrame(sendMsgId, payload, ct).ConfigureAwait(false);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);
                using (timeoutCts.Token.Register(() =>
                {
                    if (ct.IsCancellationRequested) pending.Tcs.TrySetCanceled(ct);
                    else pending.Tcs.TrySetException(new TimeoutException(
                        $"No response (msgId=0x{expectedResponseMsgId:x2}) for request {requestId} within {timeout.TotalSeconds:0}s"));
                }))
                {
                    return await pending.Tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                _pendingRequests.TryRemove(requestId, out _);
            }
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

                        _onFrameReceived();

                        var action = await _handleControlFrame(msgId, ct).ConfigureAwait(false);
                        if (action == RlpxControlFrameAction.Continue) continue;
                        if (action == RlpxControlFrameAction.PeerDisconnected)
                        {
                            _onDisconnected();
                            throw new IOException("Peer disconnected");
                        }

                        if (Nethereum.RLP.RLP.TryReadLeadingListUInt64(payload, out var reqId)
                            && _pendingRequests.TryGetValue(reqId, out var pending)
                            && pending.ExpectedMsgId == msgId)
                        {
                            _pendingRequests.TryRemove(reqId, out _);
                            pending.Tcs.TrySetResult(payload);
                        }
                        else
                        {
                            _onUnsolicited(msgId, payload);
                        }
                    }
                    fault = new IOException("connection read loop ended");
                }
                catch (Exception ex)
                {
                    fault = ex;
                }
                FaultAllPending(fault);
            }
            finally
            {
                _onDisconnected();
            }
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
                try { _loopCts?.Cancel(); } catch { }
                FaultAllPending(new ObjectDisposedException(nameof(RlpxConnection)));
                try { _loopCts?.Dispose(); } catch { }
            }
        }
    }
}
