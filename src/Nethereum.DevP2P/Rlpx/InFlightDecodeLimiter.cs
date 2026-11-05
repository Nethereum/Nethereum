using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Rlpx
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p", "InFlightDecodeLimiter — bound the bytes decoded at once")]
    public sealed class InFlightDecodeLimiter
    {
        public const long BytesPerPeer = 4L * 1024 * 1024;

        public const int DefaultMaxPeers = 25;

        public static readonly InFlightDecodeLimiter Shared =
            new InFlightDecodeLimiter(DefaultMaxPeers * BytesPerPeer);

        private sealed class Waiter
        {
            public readonly long Bytes;
            public readonly TaskCompletionSource<InFlightDecodeLease> Completion =
                new TaskCompletionSource<InFlightDecodeLease>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Waiter(long bytes) => Bytes = bytes;
        }

        private readonly object _lock = new object();
        private readonly LinkedList<Waiter> _waiters = new LinkedList<Waiter>();
        private long _inFlightBytes;

        public InFlightDecodeLimiter(long capacityBytes)
        {
            if (capacityBytes <= 0) throw new ArgumentOutOfRangeException(nameof(capacityBytes));
            CapacityBytes = capacityBytes;
        }

        public static InFlightDecodeLimiter ForMaxPeers(int maxPeers)
        {
            return new InFlightDecodeLimiter(Math.Max(1, maxPeers) * BytesPerPeer);
        }

        public long CapacityBytes { get; }

        public long InFlightBytes
        {
            get { lock (_lock) return _inFlightBytes; }
        }

        public ValueTask<InFlightDecodeLease> AcquireAsync(long bytes, CancellationToken ct = default)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            ct.ThrowIfCancellationRequested();

            Waiter waiter;
            LinkedListNode<Waiter> node;
            lock (_lock)
            {
                if (_waiters.Count == 0 && Fits(bytes))
                {
                    _inFlightBytes += bytes;
                    return new ValueTask<InFlightDecodeLease>(new InFlightDecodeLease(this, bytes));
                }
                waiter = new Waiter(bytes);
                node = _waiters.AddLast(waiter);
            }

            if (!ct.CanBeCanceled) return new ValueTask<InFlightDecodeLease>(waiter.Completion.Task);
            return new ValueTask<InFlightDecodeLease>(WaitCancellableAsync(waiter, node, ct));
        }

        private async Task<InFlightDecodeLease> WaitCancellableAsync(
            Waiter waiter, LinkedListNode<Waiter> node, CancellationToken ct)
        {
            using (ct.Register(() => Cancel(waiter, node, ct)))
                return await waiter.Completion.Task.ConfigureAwait(false);
        }

        private void Cancel(Waiter waiter, LinkedListNode<Waiter> node, CancellationToken ct)
        {
            lock (_lock)
            {
                if (waiter.Completion.Task.IsCompleted) return;
                _waiters.Remove(node);
                waiter.Completion.TrySetCanceled(ct);
                Admit();
            }
        }

        private bool Fits(long bytes)
        {
            return _inFlightBytes == 0 || _inFlightBytes + bytes <= CapacityBytes;
        }

        internal void Release(long bytes)
        {
            lock (_lock)
            {
                _inFlightBytes -= bytes;
                Admit();
            }
        }

        private void Admit()
        {
            while (_waiters.First != null && Fits(_waiters.First.Value.Bytes))
            {
                var waiter = _waiters.First.Value;
                _waiters.RemoveFirst();
                _inFlightBytes += waiter.Bytes;
                waiter.Completion.TrySetResult(new InFlightDecodeLease(this, waiter.Bytes));
            }
        }
    }

    public sealed class InFlightDecodeLease : IDisposable
    {
        private readonly InFlightDecodeLimiter _limiter;
        private readonly long _bytes;
        private int _released;

        internal InFlightDecodeLease(InFlightDecodeLimiter limiter, long bytes)
        {
            _limiter = limiter;
            _bytes = bytes;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            _limiter.Release(_bytes);
        }
    }
}
