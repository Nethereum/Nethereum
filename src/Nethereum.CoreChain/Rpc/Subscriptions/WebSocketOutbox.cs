using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Rpc.Subscriptions
{
    internal sealed class WebSocketOutbox
    {
        public const long MaxQueuedBytes = 64L * 1024 * 1024;

        private readonly WebSocket _socket;
        private readonly TimeSpan _writeTimeout;
        private readonly Action _onFailed;
        private readonly Queue<byte[]> _queue = new Queue<byte[]>();
        private long _queuedBytes;
        private bool _draining;
        private bool _failed;

        public WebSocketOutbox(WebSocket socket, TimeSpan writeTimeout, Action onFailed)
        {
            _socket = socket;
            _writeTimeout = writeTimeout;
            _onFailed = onFailed;
        }

        public bool TryEnqueue(byte[] payload)
        {
            lock (_queue)
            {
                if (_failed) return false;
                if (_queuedBytes + payload.Length > MaxQueuedBytes)
                {
                    FailLocked();
                    return false;
                }

                _queue.Enqueue(payload);
                _queuedBytes += payload.Length;
                if (_draining) return true;
                _draining = true;
            }

            _ = DrainAsync();
            return true;
        }

        private async Task DrainAsync()
        {
            while (true)
            {
                byte[] next;
                lock (_queue)
                {
                    if (_failed || _queue.Count == 0)
                    {
                        _draining = false;
                        return;
                    }

                    next = _queue.Dequeue();
                    _queuedBytes -= next.Length;
                }

                if (!await SendWithDeadlineAsync(next).ConfigureAwait(false))
                {
                    lock (_queue) FailLocked();
                    return;
                }
            }
        }

        private async Task<bool> SendWithDeadlineAsync(byte[] payload)
        {
            using var cancellation = new CancellationTokenSource();
            try
            {
                var send = _socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellation.Token);
                await send.WaitAsync(_writeTimeout).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                cancellation.Cancel();
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void FailLocked()
        {
            if (_failed) return;
            _failed = true;
            _queue.Clear();
            _queuedBytes = 0;
            _draining = false;
            try { _socket.Abort(); } catch (Exception) { }
            _onFailed();
        }
    }
}
