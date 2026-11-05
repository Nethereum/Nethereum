using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p-sync", "FollowingBlockSource — pull blocks and follow peer heads")]
    public sealed class FollowingBlockSource : IBlockSource
    {
        public static readonly TimeSpan DefaultFallbackPoll = TimeSpan.FromSeconds(1);
        public const ulong DefaultMaxRewindDepth = 1024UL;

        private readonly IBlockSource _pull;
        private readonly PeerPoolManager _pool;
        private readonly Func<ulong, Task<byte[]>> _localHashAt;
        private readonly TimeSpan _fallbackPoll;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly ulong _maxRewindDepth;
        private readonly Dictionary<Guid, byte[]> _examinedHeadByPeer = new Dictionary<Guid, byte[]>();

        public FollowingBlockSource(
            IBlockSource pull, PeerPoolManager pool, Func<ulong, Task<byte[]>> localHashAt,
            TimeSpan? fallbackPoll = null)
            : this(pull, pool, localHashAt, null, DefaultMaxRewindDepth, fallbackPoll)
        {
        }

        public FollowingBlockSource(
            IBlockSource pull, PeerPoolManager pool, Func<ulong, Task<byte[]>> localHashAt,
            IFetchRequestScheduler scheduler, ulong maxRewindDepth, TimeSpan? fallbackPoll = null)
        {
            _pull = pull ?? throw new ArgumentNullException(nameof(pull));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _localHashAt = localHashAt ?? throw new ArgumentNullException(nameof(localHashAt));
            _scheduler = scheduler;
            _maxRewindDepth = maxRewindDepth;
            _fallbackPoll = fallbackPoll ?? DefaultFallbackPoll;
        }

        public DivergenceSignal LastChainBreak => _pull.LastChainBreak;

        public async IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var next = fromBlock;
            using var headAdvanced = new SemaphoreSlim(0, 1);
            EventHandler<SyncPeerSession> onAdvanced = (_, _) => SignalHeadAdvanced(headAdvanced);
            _pool.PeerHeadAdvanced += onAdvanced;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await foreach (var bundle in _pull.StreamAsync(next, ct).WithCancellation(ct).ConfigureAwait(false))
                    {
                        next = (ulong)bundle.Header.BlockNumber + 1;
                        yield return bundle;
                    }

                    if (_pull.LastChainBreak != null) yield break;

                    var competing = await FirstConfirmedCompetingHeadAsync(next, ct).ConfigureAwait(false);
                    if (competing.HasValue)
                    {
                        next = competing.Value;
                        continue;
                    }

                    try { await headAdvanced.WaitAsync(_fallbackPoll, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { yield break; }
                }
            }
            finally
            {
                _pool.PeerHeadAdvanced -= onAdvanced;
            }
        }

        private static void SignalHeadAdvanced(SemaphoreSlim headAdvanced)
        {
            try { headAdvanced.Release(); }
            catch (SemaphoreFullException) { }
            catch (ObjectDisposedException) { }
        }

        private async Task<ulong?> FirstConfirmedCompetingHeadAsync(ulong next, CancellationToken ct)
        {
            var peers = _pool.ActivePeers.OfType<SyncPeerSession>().ToList();
            _examinedHeadByPeer.Keys.Where(id => peers.All(p => p.Id != id)).ToList()
                .ForEach(id => _examinedHeadByPeer.Remove(id));

            foreach (var peer in peers)
            {
                var number = peer.PeerLatestBlock;
                var hash = peer.PeerLatestBlockHash;
                if (hash == null || number >= next) continue;
                if (_examinedHeadByPeer.TryGetValue(peer.Id, out var examined) && ByteUtil.AreEqual(examined, hash)) continue;

                _examinedHeadByPeer[peer.Id] = hash;
                var local = await _localHashAt(number).ConfigureAwait(false);
                if (local == null || ByteUtil.AreEqual(local, hash)) continue;
                if (await IsRewindWarrantedAsync(peer, number, hash, next, ct).ConfigureAwait(false)) return number;
            }

            return null;
        }

        private async Task<bool> IsRewindWarrantedAsync(
            SyncPeerSession peer, ulong number, byte[] hash, ulong next, CancellationToken ct)
        {
            var depth = next - number;
            if (depth <= 1) return true;
            if (_scheduler == null || number == 0 || depth > _maxRewindDepth) return false;

            var header = await FetchAnnouncedHeaderAsync(number, hash, ct).ConfigureAwait(false);
            if (header == null)
            {
                _scheduler.QuarantineHeaderPeer(peer.Id);
                return false;
            }

            var parent = await _localHashAt(number - 1).ConfigureAwait(false);
            return parent != null && ByteUtil.AreEqual(parent, header.ParentHash);
        }

        private async Task<BlockHeader> FetchAnnouncedHeaderAsync(ulong number, byte[] hash, CancellationToken ct)
        {
            try
            {
                var headers = await _scheduler.FetchHeadersByHashAsync(hash, 1, ct).ConfigureAwait(false);
                if (headers == null || headers.Count == 0) return null;
                var header = headers[0];
                if ((ulong)header.BlockNumber != number || header.ParentHash == null) return null;
                return header;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;
            }
        }

        public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => _pull.GetHealthAsync(ct);

        public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct) =>
            _pull.ReportBadBundleAsync(blockNumber, reason, ct);
    }
}
