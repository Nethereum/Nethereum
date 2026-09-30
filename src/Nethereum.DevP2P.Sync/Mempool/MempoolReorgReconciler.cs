using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Mempool
{
    public sealed class MempoolReorgReconciler : IMempoolReorgReconciler
    {
        private readonly ITxPool _txPool;
        private readonly RelayMempool _relay;
        private readonly ILogger _logger;

        public MempoolReorgReconciler(ITxPool txPool, RelayMempool relay, ILogger<MempoolReorgReconciler> logger = null)
        {
            _txPool = txPool ?? throw new ArgumentNullException(nameof(txPool));
            _relay = relay ?? throw new ArgumentNullException(nameof(relay));
            _logger = (ILogger)logger ?? NullLogger.Instance;
        }

        public async Task ReconcileAsync(
            IChainStoreBundle bundle,
            ulong commonAncestor,
            ulong orphanedHead,
            IReadOnlyList<ISignedTransaction> winningBranchTransactions,
            CancellationToken ct)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            var discarded = await CollectOrphanedBranchTransactionsAsync(bundle, commonAncestor, orphanedHead, ct)
                .ConfigureAwait(false);
            var included = winningBranchTransactions ?? Array.Empty<ISignedTransaction>();

            var includedHashHexes = new HashSet<string>(
                included.Where(tx => tx?.Hash != null).Select(tx => tx.Hash.ToHex()));

            var reinject = discarded
                .Where(tx => tx?.Hash != null && !includedHashHexes.Contains(tx.Hash.ToHex()))
                .ToList();

            var includedHashes = included.Where(tx => tx?.Hash != null).Select(tx => tx.Hash).ToList();
            if (includedHashes.Count > 0)
                await _txPool.RemoveBatchAsync(includedHashes).ConfigureAwait(false);

            if (reinject.Count > 0)
                await _relay.AdmitFromTrustedPeerAsync(reinject, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "mempool.reorg_reconciled ancestor={Ancestor} orphaned_head={OrphanedHead} " +
                "discarded={Discarded} included={Included} reinjected={Reinjected}",
                commonAncestor, orphanedHead, discarded.Count, included.Count, reinject.Count);
        }

        private static async Task<List<ISignedTransaction>> CollectOrphanedBranchTransactionsAsync(
            IChainStoreBundle bundle, ulong commonAncestor, ulong orphanedHead, CancellationToken ct)
        {
            var collected = new List<ISignedTransaction>();
            for (var number = commonAncestor + 1; number <= orphanedHead; number++)
            {
                ct.ThrowIfCancellationRequested();
                var txs = await bundle.Transactions.GetByBlockNumberAsync((BigInteger)number).ConfigureAwait(false);
                if (txs != null) collected.AddRange(txs);
            }
            return collected;
        }
    }
}
