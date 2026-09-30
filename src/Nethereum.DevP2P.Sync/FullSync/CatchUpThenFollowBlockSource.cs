using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;

namespace Nethereum.DevP2P.Sync.FullSync
{
    /// <summary>
    /// Sync as two phases behind one source: PULL history until it runs out, then FOLLOW the live feed.
    /// <para>
    /// A pull source ends its stream at the peer's head, so a follower driven by one alone exits the
    /// moment it catches up and never sees another block. A push source never ends, but cannot supply
    /// history a node missed. Neither is sync on its own; this is.
    /// </para>
    /// </summary>
    public sealed class CatchUpThenFollowBlockSource : IBlockSource
    {
        private readonly IBlockSource _catchUp;
        private readonly IBlockSource _live;

        public CatchUpThenFollowBlockSource(IBlockSource catchUp, IBlockSource live)
        {
            _catchUp = catchUp ?? throw new ArgumentNullException(nameof(catchUp));
            _live = live ?? throw new ArgumentNullException(nameof(live));
        }

        public bool CaughtUp { get; private set; }

        public DivergenceSignal LastChainBreak => _catchUp.LastChainBreak ?? _live.LastChainBreak;

        public async IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var next = fromBlock;

            while (!ct.IsCancellationRequested)
            {
                await foreach (var bundle in _catchUp.StreamAsync(next, ct).WithCancellation(ct).ConfigureAwait(false))
                {
                    next = (ulong)bundle.Header.BlockNumber + 1;
                    yield return bundle;
                }

                CaughtUp = true;

                await foreach (var bundle in _live.StreamAsync(next, ct).WithCancellation(ct).ConfigureAwait(false))
                {
                    next = (ulong)bundle.Header.BlockNumber + 1;
                    yield return bundle;
                }
            }
        }

        public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) =>
            CaughtUp ? _live.GetHealthAsync(ct) : _catchUp.GetHealthAsync(ct);

        public async Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
        {
            await _catchUp.ReportBadBundleAsync(blockNumber, reason, ct).ConfigureAwait(false);
            await _live.ReportBadBundleAsync(blockNumber, reason, ct).ConfigureAwait(false);
        }
    }
}
