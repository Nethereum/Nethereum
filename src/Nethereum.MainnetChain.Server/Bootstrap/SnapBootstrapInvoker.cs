using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Server.Bootstrap
{
    /// <summary>
    /// Hosted-service hook that runs <see cref="SnapBootstrapper.RunAsync"/>
    /// before the follower's first <c>RunAsync</c> call when:
    /// (a) <see cref="MainnetChainServerConfig.SnapBootstrap"/> is true, AND
    /// (b) the bundle has no committed state (<see cref="IChainMetadataStore.GetLastBlock"/> == 0), AND
    /// (c) a trusted peer is configured.
    ///
    /// <para>
    /// Dials the trusted peer directly via <see cref="MainnetPeerSession.ConnectAsync"/>
    /// rather than going through the <c>PeerPoolManager</c>. Reasons:
    /// 1. The pool starts dialing in parallel during runtime init; the snap-bootstrap
    ///    must complete before the follower starts, so it cannot wait for the pool
    ///    to discover a peer with snap/1 negotiated.
    /// 2. The pool's per-peer-session lifecycle is owned by the pool — taking it
    ///    out-of-band for a long snap stream and returning it cleanly is more wiring
    ///    than a single direct dial.
    /// 3. A separate dial isolates snap-bootstrap failure modes from the follower's
    ///    peer pool — if the bootstrap fails, the pool's regular peers are
    ///    unaffected and the follower can still do a full-replay sync.
    /// </para>
    ///
    /// <para>
    /// Pivot selection: <c>peerLatest - <see cref="MainnetChainServerConfig.SnapPivotDistance"/></c>
    /// (default 128). Geth's reorg buffer is the same shape. Going too close to tip
    /// risks a reorg moving the pivot underneath us; too far back gives a longer
    /// catch-up phase after snap completes.
    /// </para>
    /// </summary>
    public static class SnapBootstrapInvoker
    {
        public static async Task<SnapBootstrapper.Result> RunIfConfiguredAsync(
            IChainStoreBundle bundle,
            MainnetChainServerConfig config,
            ILogger logger,
            CancellationToken ct)
        {
            if (!config.SnapBootstrap)
                return new SnapBootstrapper.Result { Ran = false, SkipReason = "SnapBootstrap config flag is false" };

            var lastBlock = bundle.Metadata.GetLastBlock();
            if (lastBlock > 0)
                return new SnapBootstrapper.Result { Ran = false, SkipReason = $"existing state at block {lastBlock}" };

            if (string.IsNullOrWhiteSpace(config.TrustedPeer))
            {
                logger.LogWarning("Snap-bootstrap skipped: SnapBootstrap=true but no TrustedPeer is configured.");
                return new SnapBootstrapper.Result { Ran = false, SkipReason = "no trusted peer configured" };
            }

            logger.LogInformation(
                "Snap-bootstrap: dialing trusted peer for pivot discovery (pivot_distance={Distance})...",
                config.SnapPivotDistance);

            MainnetPeerSession session = null;
            try
            {
                session = await MainnetPeerSession.ConnectAsync(
                    config.TrustedPeer,
                    timeout: TimeSpan.FromSeconds(30),
                    ct: ct).ConfigureAwait(false);

                var peerLatest = session.PeerLatestBlock;
                if (peerLatest <= config.SnapPivotDistance)
                {
                    logger.LogWarning(
                        "Snap-bootstrap skipped: peer latest={Latest} <= pivot distance={Distance}; cannot pick a pivot.",
                        peerLatest, config.SnapPivotDistance);
                    return new SnapBootstrapper.Result { Ran = false, SkipReason = "peer too close to genesis" };
                }

                var pivotBlock = peerLatest - config.SnapPivotDistance;
                logger.LogInformation(
                    "Snap-bootstrap: peer_latest={PeerLatest}, pivot_block={PivotBlock}; fetching header...",
                    peerLatest, pivotBlock);

                var headers = await session.GetHeadersAsync(pivotBlock, limit: 1, ct).ConfigureAwait(false);
                if (headers == null || headers.Count == 0)
                {
                    logger.LogWarning(
                        "Snap-bootstrap skipped: peer returned no header for pivot block {PivotBlock}.",
                        pivotBlock);
                    return new SnapBootstrapper.Result { Ran = false, SkipReason = "peer returned no pivot header" };
                }

                var pivotHeader = headers[0];
                var pivotHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(pivotHeader);

                logger.LogInformation(
                    "Snap-bootstrap: pivot header received block={Block} hash=0x{Hash} stateRoot=0x{Root}; running snap stream...",
                    pivotHeader.BlockNumber, pivotHash.ToHex(), pivotHeader.StateRoot.ToHex());

                var peer = new Eth68SnapPeer(session);
                var result = await SnapBootstrapper.RunAsync(
                    bundle, peer, pivotHeader, pivotHash, logger, ct).ConfigureAwait(false);

                logger.LogInformation(
                    "Snap-bootstrap: completed at pivot block {Block} ({Accounts} accounts, {Slots} slots, {Codes} bytecodes).",
                    result.PivotBlockNumber, result.AccountCount, result.SlotCount, result.BytecodeCount);
                return result;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex,
                    "Snap-bootstrap failed; falling back to full-replay sync from genesis. " +
                    "Restarting the server with SnapBootstrap=true will retry only if the data dir is still empty.");
                return new SnapBootstrapper.Result { Ran = false, SkipReason = $"failed: {ex.GetType().Name}: {ex.Message}" };
            }
            finally
            {
                if (session != null)
                {
                    try { await session.DisposeAsync().ConfigureAwait(false); }
                    catch { /* best-effort */ }
                }
            }
        }
    }
}
