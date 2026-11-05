using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    internal sealed class PeerQuarantine
    {
        private readonly IPeerPool _pool;
        private readonly ILogger<FetchRequestScheduler> _logger;
        private readonly Func<TimeSpan> _headerBatchQuarantineDuration;

        private readonly ConcurrentDictionary<(Guid PeerId, TimeoutStreakKind Kind), int> _consecutiveTimeouts = new();
        private const int RepeatedTimeoutQuarantineThreshold = 3;

        private const int TrustedRepeatedTimeoutQuarantineThreshold = 20;

        private readonly ConcurrentDictionary<Guid, long> _snapStateQuarantineUntilTicks = new();
        private static readonly TimeSpan SnapStateQuarantineDuration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan TrustedSnapStateCooldown = TimeSpan.FromSeconds(20);

        private static readonly TimeSpan BehindSnapStateCooldown = TimeSpan.FromSeconds(6);
        private const ulong SnapStateBehindMargin = 4;

        private readonly ConcurrentDictionary<Guid, long> _headerQuarantineUntilTicks = new();

        private readonly ConcurrentDictionary<Guid, long> _claimDemotedUntilTicks = new();

        private readonly ConcurrentDictionary<Guid, int> _snapQuarantineCycles = new();
        private const int SnapQuarantineDropThreshold = 3;
        private const int TrustedSnapQuarantineDropThreshold = 20;

        private static readonly byte[] NoRoot = new byte[32];

        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<byte[], byte>> _statelessPeers = new();

        private byte[]? _currentStateRoot;

        internal PeerQuarantine(
            IPeerPool pool,
            ILogger<FetchRequestScheduler> logger,
            Func<TimeSpan> headerBatchQuarantineDuration)
        {
            _pool = pool;
            _logger = logger;
            _headerBatchQuarantineDuration = headerBatchQuarantineDuration;
        }

        internal bool RegisterTimeout(Guid peerId, TimeoutStreakKind kind, bool isTrusted, out int triggeredAtCount)
        {
            var key = (peerId, kind);
            var count = _consecutiveTimeouts.AddOrUpdate(key, 1, (_, prev) => prev + 1);
            triggeredAtCount = count;
            var threshold = isTrusted ? TrustedRepeatedTimeoutQuarantineThreshold : RepeatedTimeoutQuarantineThreshold;
            if (count < threshold) return false;
            _consecutiveTimeouts[key] = 0;
            return true;
        }

        internal void RegisterSuccess(Guid peerId, TimeoutStreakKind kind)
        {
            _consecutiveTimeouts.TryRemove((peerId, kind), out _);
            if (kind == TimeoutStreakKind.Snap) _snapQuarantineCycles.TryRemove(peerId, out _);
        }

        internal void DropOnRepeatedEthTimeout(IEthPeer peer)
        {
            _logger.LogWarning(
                "eth.peer.timeout.drop peer={Host} — repeated eth timeout, dropping for redial",
                SyncPeerSession.ParseHost(peer.Enode));
            _ = _pool.DropAsync(peer.Id, "repeated eth timeout", CancellationToken.None);
        }

        internal int ConsecutiveTimeouts(Guid peerId, TimeoutStreakKind kind)
            => _consecutiveTimeouts.TryGetValue((peerId, kind), out var n) ? n : 0;

        internal int SnapQuarantineCycles(Guid peerId)
            => _snapQuarantineCycles.TryGetValue(peerId, out var n) ? n : 0;

        internal void Forget(Guid peerId)
        {
            _headerQuarantineUntilTicks.TryRemove(peerId, out _);
            _claimDemotedUntilTicks.TryRemove(peerId, out _);
            _consecutiveTimeouts.TryRemove((peerId, TimeoutStreakKind.Eth), out _);
            _consecutiveTimeouts.TryRemove((peerId, TimeoutStreakKind.Snap), out _);
            _snapQuarantineCycles.TryRemove(peerId, out _);
            _statelessPeers.TryRemove(peerId, out _);
            _snapStateQuarantineUntilTicks.TryRemove(peerId, out _);
        }

        private ulong NetworkHeadBlock() => _pool.ActivePeers
            .OfType<SyncPeerSession>()
            .Select(s => s.PeerLatestBlock)
            .DefaultIfEmpty(0UL)
            .Max();

        internal static TimeSpan ChooseSnapStateCooldown(ulong peerLatestBlock, ulong networkHeadBlock, bool isTrusted)
        {
            bool behind = networkHeadBlock > SnapStateBehindMargin
                          && peerLatestBlock + SnapStateBehindMargin < networkHeadBlock;
            if (behind) return BehindSnapStateCooldown;
            return isTrusted ? TrustedSnapStateCooldown : SnapStateQuarantineDuration;
        }

        internal void QuarantineHeaderPeer(Guid peerId) =>
            _headerQuarantineUntilTicks[peerId] = DateTime.UtcNow.Add(_headerBatchQuarantineDuration()).Ticks;

        internal bool IsHeaderPeerQuarantined(Guid peerId) =>
            _headerQuarantineUntilTicks.TryGetValue(peerId, out var untilTicks)
            && DateTime.UtcNow.Ticks < untilTicks;

        internal void DemoteClaim(Guid peerId) =>
            _claimDemotedUntilTicks[peerId] = DateTime.UtcNow.Add(_headerBatchQuarantineDuration()).Ticks;

        internal bool IsClaimDemoted(Guid peerId) =>
            _claimDemotedUntilTicks.TryGetValue(peerId, out var untilTicks)
            && DateTime.UtcNow.Ticks < untilTicks;

        internal bool HeaderServingFilter(IEthPeer p) => !IsHeaderPeerQuarantined(p.Id);

        internal void QuarantineSnapState(IEthPeer peer)
        {
            _snapStateQuarantineUntilTicks[peer.Id] = DateTime.UtcNow
                .Add(ChooseSnapStateCooldown(peer.PeerLatestBlock, NetworkHeadBlock(), peer.IsTrusted)).Ticks;

            var threshold = peer.IsTrusted ? TrustedSnapQuarantineDropThreshold : SnapQuarantineDropThreshold;
            var cycles = _snapQuarantineCycles.AddOrUpdate(peer.Id, 1, (_, prev) => prev + 1);
            if (cycles < threshold) return;

            _snapQuarantineCycles.TryRemove(peer.Id, out _);
            _logger.LogWarning(
                "snap.peer.quarantine.drop peer={Host} — repeated snap quarantine with no intervening success, dropping for redial",
                SyncPeerSession.ParseHost(peer.Enode));
            _ = _pool.DropAsync(peer.Id, "repeated snap quarantine", CancellationToken.None);
        }

        internal bool IsSnapStateQuarantined(Guid peerId) =>
            _snapStateQuarantineUntilTicks.TryGetValue(peerId, out var untilTicks)
            && DateTime.UtcNow.Ticks < untilTicks;

        internal void ClearSnapStateBenches()
        {
            if (_snapStateQuarantineUntilTicks.IsEmpty) return;
            var benched = _snapStateQuarantineUntilTicks.Count;
            _snapStateQuarantineUntilTicks.Clear();
            _logger.LogInformation(
                "snap.quarantine.cleared count={Count} — target root rotated; benched peers re-evaluated against the new root",
                benched);
        }

        internal bool SnapStateServingFilter(IEthPeer p) =>
            p is SyncPeerSession ms && ms.SupportsSnap && !IsSnapStateQuarantined(ms.Id);

        internal byte[] RootOfLastRootRequest => Volatile.Read(ref _currentStateRoot) ?? NoRoot;

        internal void NoteRequestRoot(byte[] stateRoot) => Volatile.Write(ref _currentStateRoot, stateRoot);

        internal void ClearStateless()
        {
            _statelessPeers.Clear();
            Volatile.Write(ref _currentStateRoot, null);
        }

        internal StatelessReplyException MarkStateless(IEthPeer peer, byte[] stateRoot, string request)
        {
            _statelessPeers.GetOrAdd(peer.Id, _ => new ConcurrentDictionary<byte[], byte>(Nethereum.Util.ByteArrayComparer.Current))[stateRoot] = 0;
            _logger.LogInformation(
                "snap.peer.stateless peer={Host} root=0x{Root} request={Request} — peer answered empty; not asked again at this root",
                SyncPeerSession.ParseHost(peer.Enode), FetchRequestScheduler.HexShort(stateRoot), request);
            return new StatelessReplyException(
                $"{request}: peer {SyncPeerSession.ParseHost(peer.Enode)} answered empty (stateless at root 0x{FetchRequestScheduler.HexShort(stateRoot)})");
        }

        internal bool IsStateless(IEthPeer peer, byte[] stateRoot)
            => _statelessPeers.TryGetValue(peer.Id, out var roots) && roots.ContainsKey(stateRoot);

        internal bool EverySnapPeerIsStateless(byte[] stateRoot)
        {
            var anySnapPeer = false;
            foreach (var peer in _pool.ActivePeers)
            {
                if (peer is not SyncPeerSession ms || !ms.SupportsSnap) continue;
                if (!IsStateless(ms, stateRoot)) return false;
                anySnapPeer = true;
            }
            return anySnapPeer;
        }
    }
}
