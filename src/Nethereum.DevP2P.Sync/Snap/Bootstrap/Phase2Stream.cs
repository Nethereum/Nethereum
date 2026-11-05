using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class Phase2Stream
    {
        private readonly SnapRunContext _context;
        private readonly SnapSyncClient _client;
        private readonly SnapSyncState _resumeFrom;
        private readonly Action<SnapSyncClient.SnapSyncCheckpoint> _checkpointSink;

        internal Phase2Stream(
            SnapRunContext ctx, SnapSyncClient client, SnapSyncState resumeFrom,
            Action<SnapSyncClient.SnapSyncCheckpoint> checkpointSink)
        {
            _context = ctx;
            _client = client;
            _resumeFrom = resumeFrom;
            _checkpointSink = checkpointSink;
        }

        internal async Task<SnapSyncClient.SyncResult> StreamAsync(CancellationToken ct)
        {
            if (_context.PivotRefresher != null)
            {
                _client.PivotRefresher = _context.Pivots.AsClientRefresher();
            }

            var targetRoot = await _context.Pivots.AnchorFreshAtStartAsync(_context.Pivot, ct).ConfigureAwait(false);

            return await _client.StreamLeavesAsync(targetRoot, _resumeFrom, _checkpointSink, ct).ConfigureAwait(false);
        }
    }
}
