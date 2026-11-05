using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class FrozenRoot
    {
        public static readonly FrozenRoot Unpinned = new FrozenRoot(new byte[1][], pinned: false);

        private readonly byte[][] _current;
        private readonly bool _pinned;
        private readonly object _attemptLock = new object();
        private byte[] _pending;
        private CancellationTokenSource _attemptCts;
        private int _refreshing;

        public FrozenRoot(byte[] root)
            : this(new byte[][] { root }, pinned: true) { }

        private FrozenRoot(byte[][] current, bool pinned)
        {
            _current = current;
            _pinned = pinned;
        }

        public static FrozenRoot Wrap(byte[][] holder)
            => holder == null ? Unpinned : new FrozenRoot(holder, pinned: true);

        public byte[] Current
        {
            get => Volatile.Read(ref _current[0]);
            set => Volatile.Write(ref _current[0], value);
        }

        public byte[] Pending => Volatile.Read(ref _pending);

        public bool Diverged(byte[] root)
            => _pinned && !ByteUtil.AreEqual(root, Current);

        public void BeginAttempt(CancellationTokenSource attemptCts)
        {
            CancellationTokenSource previous;
            lock (_attemptLock)
            {
                previous = _attemptCts;
                _attemptCts = attemptCts;
                Volatile.Write(ref _pending, null);
            }
            previous?.Dispose();
        }

        public void NoteMove(byte[] fresh)
        {
            lock (_attemptLock)
            {
                Volatile.Write(ref _pending, fresh);
                _attemptCts?.Cancel();
            }
        }

        public void CancelCurrent()
        {
            lock (_attemptLock) { _attemptCts?.Cancel(); }
        }

        public void DisposeCurrent()
        {
            lock (_attemptLock) { _attemptCts?.Dispose(); _attemptCts = null; }
        }

        public async Task RunDriverAsync(
            SnapSyncClient client,
            CancellationTokenSource rootRollCts,
            Func<CancellationToken, Task<byte[]>> rootRefresher)
        {
            while (!rootRollCts.Token.IsCancellationRequested)
            {
                try { await Task.Delay(client.RootRefreshIntervalMs, rootRollCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (Interlocked.Exchange(ref _refreshing, 1) != 0) continue;
                try
                {
                    var (timedOut, fresh) = await TimeBounded.RunAsync(
                        rootRefresher, TimeSpan.FromMilliseconds(client.RootRefreshIntervalMs), rootRollCts.Token).ConfigureAwait(false);
                    if (timedOut)
                    {
                        client.Logger.LogWarning("snap.pivot.refresh_timeout after {Ms}ms — retrying next tick", client.RootRefreshIntervalMs);
                        continue;
                    }
                    if (fresh != null && fresh.Length == 32
                        && !ByteUtil.AreEqual(Current, fresh))
                    {
                        NoteMove(fresh);
                        client.Logger.LogInformation("snap.pivot.move_detected new_root=0x{Root}", fresh.ToHex());
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    client.Logger.LogWarning(ex, "snap.pivot.refresh_error after_ms={Ms} error_type={ErrorType}", client.RootRefreshIntervalMs, ex.GetType().Name);
                }
                finally { Volatile.Write(ref _refreshing, 0); }
            }
        }
    }
}
