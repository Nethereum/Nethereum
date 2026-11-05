using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    internal sealed record FetchRequest<T>(
        string Label,
        Func<IEthPeer, CancellationToken, Task<T>> Send,
        IReadOnlyCollection<Guid> InitialExcluded,
        Func<IEthPeer, bool> PeerFilter,
        Action<IEthPeer> OnRepeatedTimeout,
        TimeoutStreakKind Kind,
        Func<bool> NoPeerCanServe,
        byte[] StatelessRoot,
        Func<T, int> Delivered = null);

    internal static class FetchRequest
    {
        public static FetchRequest<T> Eth<T>(
            string label,
            Func<IEthPeer, CancellationToken, Task<T>> send,
            IReadOnlyCollection<Guid> initialExcluded,
            Func<IEthPeer, bool> peerFilter,
            Action<IEthPeer> onRepeatedTimeout,
            Func<T, int> delivered)
            => new FetchRequest<T>(
                label, send, initialExcluded, peerFilter, onRepeatedTimeout,
                TimeoutStreakKind.Eth, null, null, delivered);

        public static FetchRequest<T> Snap<T>(
            string label,
            Func<IEthPeer, CancellationToken, Task<T>> send,
            Func<IEthPeer, bool> peerFilter,
            Action<IEthPeer> onRepeatedTimeout,
            byte[] statelessRoot,
            Func<bool> noPeerCanServe = null)
            => new FetchRequest<T>(
                label, send, null, peerFilter, onRepeatedTimeout,
                TimeoutStreakKind.Snap, noPeerCanServe, statelessRoot);
    }
}
