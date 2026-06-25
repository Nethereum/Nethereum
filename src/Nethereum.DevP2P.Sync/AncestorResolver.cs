using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Binary-searches for the highest block whose hash agrees across the
    /// local <see cref="IChainStoreBundle"/> and the canonical chain (per
    /// peer-fetched headers via <see cref="IFetchRequestScheduler"/>). The
    /// recovery primitive consumed by <c>FollowerService</c> on
    /// <see cref="Nethereum.CoreChain.Sync.WalkerExitReason.LastKnownGoodDivergence"/>.
    /// </summary>
    public interface IAncestorResolver
    {
        /// <summary>
        /// Binary-search the inclusive range <c>[floorBlock, divergedBlock]</c>
        /// for the last block whose hash matches across local store and the
        /// canonical chain. Returns the highest matching block number — the
        /// rewind target. When no match exists in the range, returns
        /// <paramref name="floorBlock"/> as the conservative anchor (caller
        /// rewinds to a known-good baseline).
        /// </summary>
        Task<ulong> FindAsync(ulong divergedBlock, ulong floorBlock, CancellationToken ct);
    }

    public sealed class AncestorResolver : IAncestorResolver
    {
        private readonly IFetchRequestScheduler _scheduler;
        private readonly IChainStoreBundle _bundle;
        private readonly ILogger<AncestorResolver> _logger;

        public AncestorResolver(
            IFetchRequestScheduler scheduler,
            IChainStoreBundle bundle,
            ILogger<AncestorResolver>? logger = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _logger = logger ?? NullLogger<AncestorResolver>.Instance;
        }

        public async Task<ulong> FindAsync(ulong divergedBlock, ulong floorBlock, CancellationToken ct)
        {
            if (floorBlock > divergedBlock)
                throw new ArgumentException(
                    $"floorBlock ({floorBlock}) must be <= divergedBlock ({divergedBlock})",
                    nameof(floorBlock));

            ulong lo = floorBlock;
            ulong hi = divergedBlock;
            while (lo < hi)
            {
                ct.ThrowIfCancellationRequested();
                ulong mid = lo + (hi - lo + 1) / 2;

                ProbeResult probe;
                try
                {
                    probe = await ProbeWithRetryAsync(mid, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "snap.ancestor.probe_failed at block {Block}; treating as mismatch",
                        mid);
                    probe = ProbeResult.Mismatch;
                }

                if (probe == ProbeResult.Match)
                {
                    lo = mid;
                }
                else
                {
                    if (mid == 0) break;
                    hi = mid - 1;
                }
            }

            _logger.LogInformation(
                "snap.ancestor.found block={Ancestor} diverged={Diverged} floor={Floor}",
                lo, divergedBlock, floorBlock);
            return lo;
        }

        private enum ProbeResult { Match, Mismatch }

        private const int MaxProbeAttempts = 3;
        private static readonly TimeSpan ProbeRetryDelay = TimeSpan.FromMilliseconds(500);

        private async Task<ProbeResult> ProbeWithRetryAsync(ulong block, CancellationToken ct)
        {
            for (int attempt = 0; attempt < MaxProbeAttempts; attempt++)
            {
                try
                {
                    return await ProbeAsync(block, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (attempt < MaxProbeAttempts - 1)
                {
                    _logger.LogWarning(ex,
                        "snap.ancestor.probe_attempt_failed block={Block} attempt={Attempt}/{Max}",
                        block, attempt + 1, MaxProbeAttempts);
                    await Task.Delay(ProbeRetryDelay, ct).ConfigureAwait(false);
                }
            }
            // Final attempt — let exception escape to caller's catch-as-mismatch path.
            return await ProbeAsync(block, ct).ConfigureAwait(false);
        }

        private async Task<ProbeResult> ProbeAsync(ulong block, CancellationToken ct)
        {
            var peerHeaders = await _scheduler
                .FetchHeadersAsync(block, limit: 1, ct, reverse: false)
                .ConfigureAwait(false);
            if (peerHeaders == null || peerHeaders.Count == 0)
                throw new InvalidOperationException($"empty header response for block {block}");

            var peerHeader = peerHeaders[0];
            if ((ulong)peerHeader.BlockNumber.ToBigInteger() != block) return ProbeResult.Mismatch;

            var peerEncoded = BlockHeaderEncoder.Current.Encode(peerHeader);
            var peerHash = Sha3Keccack.Current.CalculateHash(peerEncoded);

            var localHash = await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(block))
                .ConfigureAwait(false);
            if (localHash == null) return ProbeResult.Mismatch;

            return ByteArrayComparer.Current.Equals(localHash, peerHash)
                ? ProbeResult.Match
                : ProbeResult.Mismatch;
        }
    }
}
