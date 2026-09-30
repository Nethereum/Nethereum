using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Mempool
{
    public sealed class RelayMempool
    {
        private readonly ITxPool _txPool;
        private readonly Eth68PeerPool _peers;
        private readonly IChainStoreBundle _bundle;
        private readonly MempoolAdmissionValidator _validator;
        private readonly ILogger _logger;
        private readonly MempoolRetention _retention;
        private readonly MempoolRelay _relay;

        public RelayMempool(
            ITxPool txPool,
            Eth68PeerPool peers,
            IChainStoreBundle bundle,
            MempoolAdmissionValidator validator,
            ILogger<RelayMempool> logger = null,
            MempoolRetention retention = MempoolRetention.Full,
            MempoolRelay relay = MempoolRelay.Ours)
        {
            _retention = retention;
            _relay = relay;
            _txPool = txPool ?? throw new ArgumentNullException(nameof(txPool));
            _peers = peers ?? throw new ArgumentNullException(nameof(peers));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = (ILogger)logger ?? NullLogger.Instance;

            if (relay == MempoolRelay.All && retention != MempoolRetention.Full)
                throw new ArgumentException(
                    "Relaying peers' transactions needs MempoolRetention.Full: the pool is what makes a repeat " +
                    "recognisable and what serves the body for a hash this node advertised.", nameof(relay));
        }

        public int PendingCount => _txPool.PendingCount;

        private bool RetainsPeerTransactions => _retention == MempoolRetention.Full;

        private bool ShouldRelayOnward(bool alreadyKnown) => _relay == MempoolRelay.All && !alreadyKnown;

        public async Task<MempoolSubmitResult> SubmitAsync(ISignedTransaction tx, CancellationToken cancellationToken = default)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));

            var admission = await _validator.ValidateAsync(tx, _bundle, _txPool, cancellationToken).ConfigureAwait(false);
            if (!admission.Accepted)
            {
                _logger.LogDebug("Mempool rejected tx: {Reason} ({Message})", admission.Reason, admission.RejectReason);
                return MempoolSubmitResult.FromRejected(admission);
            }

            var hash = await _txPool.AddAsync(tx).ConfigureAwait(false);

            if (_relay != MempoolRelay.None)
                await PropagateAsync(tx, hash, exceptPeer: null, cancellationToken).ConfigureAwait(false);

            return MempoolSubmitResult.FromAccepted(hash, admission.Sender);
        }

        public Task<int> AdmitFromTrustedPeerAsync(
            IEnumerable<ISignedTransaction> transactions,
            CancellationToken cancellationToken = default)
            => AdmitFromTrustedPeerAsync(transactions, sourcePeer: null, cancellationToken);

        public async Task<int> AdmitFromTrustedPeerAsync(
            IEnumerable<ISignedTransaction> transactions,
            Guid? sourcePeer,
            CancellationToken cancellationToken = default)
        {
            if (transactions == null) throw new ArgumentNullException(nameof(transactions));

            var admitted = 0;
            foreach (var tx in transactions)
            {
                if (tx == null) continue;

                var admission = await _validator.ValidateAsync(tx, _bundle, _txPool, cancellationToken).ConfigureAwait(false);
                if (!admission.Accepted)
                {
                    _logger.LogDebug("Mempool refused peer tx: {Reason} ({Message})", admission.Reason, admission.RejectReason);
                    continue;
                }

                if (!RetainsPeerTransactions)
                {
                    admitted++;
                    continue;
                }

                var hash = tx.Hash;
                var alreadyKnown = hash != null && await _txPool.ContainsAsync(hash).ConfigureAwait(false);

                await _txPool.AddAsync(tx).ConfigureAwait(false);
                admitted++;

                if (ShouldRelayOnward(alreadyKnown))
                    await PropagateAsync(tx, hash, sourcePeer, cancellationToken).ConfigureAwait(false);
            }

            return admitted;
        }

        public async Task<IReadOnlyList<ISignedTransaction>> GetPendingAsync(int max)
            => await _txPool.GetPendingAsync(max).ConfigureAwait(false);

        private async Task PropagateAsync(
            ISignedTransaction tx, byte[] hash, Guid? exceptPeer, CancellationToken ct)
        {
            var peers = _peers.Peers
                .Where(p => !exceptPeer.HasValue || p.Id != exceptPeer.Value)
                .ToList();
            if (peers.Count == 0) return;

            var directCount = TransactionPropagationPolicy.DirectPeerCount(peers.Count);
            var directPeers = peers.Take(directCount).ToList();
            var announcePeers = peers.Skip(directCount).ToList();

            try
            {
                await SendFullBodiesAsync(directPeers, tx, ct).ConfigureAwait(false);
                await AnnounceHashesAsync(announcePeers, tx, hash, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Mempool propagation failed for tx 0x{Hash}",
                    Convert.ToHexString(hash).ToLowerInvariant());
            }
        }

        private Task SendFullBodiesAsync(IReadOnlyList<Eth68PeerSession> peers, ISignedTransaction tx, CancellationToken ct)
        {
            if (peers.Count == 0) return Task.CompletedTask;
            var payload = TransactionsMessageEncoder.Encode(
                new TransactionsMessage { Transactions = new List<ISignedTransaction> { tx } });
            return _peers.SendToPeersAsync(peers, Eth68MessageIds.Transactions, payload, ct);
        }

        private Task AnnounceHashesAsync(IReadOnlyList<Eth68PeerSession> peers, ISignedTransaction tx, byte[] hash, CancellationToken ct)
        {
            if (peers.Count == 0) return Task.CompletedTask;
            var announcedSize = tx is Transaction4844 blobTx
                ? blobTx.GetRLPEncodedWithSidecar().Length
                : tx.GetRLPEncoded().Length;
            var announcement = new NewPooledTransactionHashesMessage
            {
                Types = new[] { (byte)tx.TransactionType },
                Sizes = new List<long> { announcedSize },
                Hashes = new List<byte[]> { hash },
            };
            var payload = NewPooledTransactionHashesMessageEncoder.Encode(announcement);
            return _peers.SendToPeersAsync(peers, Eth68MessageIds.NewPooledTransactionHashes, payload, ct);
        }
    }
}
