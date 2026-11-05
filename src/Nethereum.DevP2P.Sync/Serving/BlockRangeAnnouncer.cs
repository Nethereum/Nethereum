using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Serving
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p-sync", "BlockRangeAnnouncer — announce the head with BlockRangeUpdate")]
    public sealed class BlockRangeAnnouncer : IAsyncDisposable
    {
        public const int DefaultAnnounceInterval = 32;
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

        private readonly Eth68PeerPool _pool;
        private readonly Func<Task<BlockRangeUpdateMessage>> _head;
        private readonly TimeSpan _pollInterval;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private Task _loop = Task.CompletedTask;
        private ulong _lastAnnouncedLatest;

        public BlockRangeAnnouncer(
            Eth68PeerPool pool,
            Func<Task<BlockRangeUpdateMessage>> head,
            int announceInterval = DefaultAnnounceInterval,
            TimeSpan? pollInterval = null,
            ILogger logger = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _head = head ?? throw new ArgumentNullException(nameof(head));
            AnnounceInterval = announceInterval;
            _pollInterval = pollInterval ?? DefaultPollInterval;
            _logger = logger ?? NullLogger.Instance;
        }

        public int AnnounceInterval { get; set; }

        public async Task StartAsync()
        {
            var range = await _head().ConfigureAwait(false);
            _lastAnnouncedLatest = range?.LatestBlock ?? 0UL;
            _loop = Task.Run(RunAsync);
        }

        public void Signal() => _signal.Release();

        public async Task AnnounceIfDueAsync(CancellationToken ct = default)
        {
            var range = await _head().ConfigureAwait(false);
            if (range == null) return;

            if (!IsDue(_lastAnnouncedLatest, range.LatestBlock)) return;

            var peers = _pool.Peers.Where(peer => NegotiatedVersion(peer) >= 69).ToList();
            if (peers.Count == 0) return;
            _lastAnnouncedLatest = range.LatestBlock;

            await _pool.SendToPeersAsync(
                peers, EthMessageIds.BlockRangeUpdate, BlockRangeUpdateMessageEncoder.Encode(range), ct)
                .ConfigureAwait(false);
            _logger.LogDebug("BlockRangeUpdate latest={Latest} sent to {Peers} peer(s)", range.LatestBlock, peers.Count);
        }

        private bool IsDue(ulong previousLatest, ulong latest) =>
            latest < previousLatest || latest - previousLatest >= (ulong)Math.Max(1, AnnounceInterval);

        private static int NegotiatedVersion(Eth68PeerSession peer) =>
            Math.Max(peer.EthVersion, peer.RemoteStatus?.ProtocolVersion ?? 0);

        private async Task RunAsync()
        {
            var ct = _stop.Token;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(_pollInterval, ct).ConfigureAwait(false);
                    await AnnounceIfDueAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BlockRangeUpdate announce failed");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            try { await _loop.ConfigureAwait(false); }
            catch { }
            _stop.Dispose();
            _signal.Dispose();
        }
    }
}
