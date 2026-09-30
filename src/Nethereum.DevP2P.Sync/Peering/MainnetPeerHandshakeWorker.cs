using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Sync.Peering
{
    public sealed class MainnetPeerHandshakeWorker : IPeerHandshakeWorker
    {
        private readonly ILogger _logger;
        private readonly EthECKey _localKey;
        private readonly Func<Task<(ulong HeadBlock, ulong HeadTime)>> _ourHeadProvider;

        /// <param name="localKey">Node identity reused for every dial this worker makes. Null
        /// falls back to a fresh ephemeral key per dial (the old behavior) — callers that need a
        /// stable enode (so a peer can durably admin_addTrustedPeer this node) must supply the
        /// node's persisted key here.</param>
        /// <param name="ourHeadProvider">
        /// Resolves OUR OWN chain head (block number + its timestamp) fresh on every dial — the
        /// composition root supplies this from its <c>IChainStoreBundle</c> (the same source
        /// <c>MainnetNodeComposition.BuildLocalStatusTemplateAsync</c> uses for the inbound side).
        /// Required for a correct EIP-2124 fork-ID: it must be computed
        /// from the local head, never a peer's. Null (the default) means "no chain to consult" and
        /// reports head (0, 0) — an honest genesis-only fork-ID, not a guess.
        /// </param>
        public MainnetPeerHandshakeWorker(
            ILogger logger = null, EthECKey localKey = null,
            Func<Task<(ulong HeadBlock, ulong HeadTime)>> ourHeadProvider = null)
        {
            _logger = logger;
            _localKey = localKey;
            _ourHeadProvider = ourHeadProvider;
        }

        public async Task<IEthPeer> HandshakeAsync(
            string enode,
            TimeSpan timeout,
            ulong minPeerLatestBlock,
            CancellationToken ct)
        {
            var (ourHeadBlock, ourHeadTime) = _ourHeadProvider != null
                ? await _ourHeadProvider().ConfigureAwait(false)
                : (0UL, 0UL);

            return await SyncPeerSession.ConnectAsync(
                    enode, timeout, ct,
                    MainnetGenesisConstants.BlockHashHex.HexToByteArray(), (ulong)MainnetGenesisConstants.ChainId,
                    minPeerLatestBlock, logger: _logger, localKey: _localKey,
                    forkBlocks: MainnetChainSchedule.ForkIdentity.BlockHeights, forkTimestamps: MainnetChainSchedule.ForkIdentity.Timestamps,
                    ourHeadBlockNumber: ourHeadBlock, ourHeadTimestamp: ourHeadTime)
                .ConfigureAwait(false);
        }
    }
}
