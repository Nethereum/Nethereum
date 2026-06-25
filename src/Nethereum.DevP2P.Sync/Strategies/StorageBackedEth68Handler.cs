using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util.HashProviders;

namespace Nethereum.DevP2P.Sync.Strategies
{
    /// <summary>
    /// Serves eth/68 + eth/69 read requests from CoreChain block/tx/receipt stores.
    /// Used when our node is a block source for incoming peers (other
    /// followers, AppChain replicas).
    /// <para>
    /// Missing-data semantics:
    /// blocks the local store does not have (typically because the requested
    /// height is above our sync cursor) are silently omitted from the response
    /// rather than padded with empty entries or surfaced as an error. The peer
    /// then retries against another server.
    /// </para>
    /// </summary>
    public class StorageBackedEth68Handler : IEth68RequestHandler
    {
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ITxPool _txPool;
        private readonly ILogger<StorageBackedEth68Handler> _logger;

        /// <summary>
        /// Cap GetPooledTransactions response size. Per eth/68 spec the
        /// response SHOULD stay under ~2 MiB. We stop accumulating once
        /// the running raw-RLP total crosses this threshold; remaining
        /// requested hashes are silently dropped (peers are allowed to
        /// request again).
        /// </summary>
        private const int PooledTxResponseSoftCapBytes = 2 * 1024 * 1024;

        public StorageBackedEth68Handler(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ITxPool txPool = null,
            ILogger<StorageBackedEth68Handler> logger = null)
        {
            _blockStore = blockStore;
            _transactionStore = transactionStore;
            _receiptStore = receiptStore;
            _txPool = txPool;
            _logger = logger ?? NullLogger<StorageBackedEth68Handler>.Instance;
        }

        public async Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
        {
            var headers = new List<BlockHeader>();

            BlockHeader origin;
            if (request.StartBlockHash != null && request.StartBlockHash.Length == 32)
            {
                origin = await _blockStore.GetByHashAsync(request.StartBlockHash);
                if (origin == null)
                {
                    _logger.LogDebug(
                        "skipped GetBlockHeaders: requested origin hash {Hash} not in local store",
                        request.StartBlockHash.ToHex(true));
                    return headers;
                }
            }
            else
            {
                origin = await _blockStore.GetByNumberAsync((long)request.StartBlock);
                if (origin == null)
                {
                    var height = await _blockStore.GetHeightAsync();
                    var requested = new BigInteger(request.StartBlock);
                    var delta = requested - height;
                    _logger.LogDebug(
                        "skipped GetBlockHeaders: requested origin block {Requested} exceeds local cursor {Height} by {Delta}",
                        request.StartBlock, height, delta);
                    return headers;
                }
            }

            headers.Add(origin);

            var step = (long)(request.Skip + 1);
            var direction = request.Reverse ? -1 : 1;
            var current = (long)origin.BlockNumber + direction * step;

            for (ulong i = 1; i < request.Limit; i++)
            {
                if (current < 0) break;
                var header = await _blockStore.GetByNumberAsync(current);
                if (header == null) break;
                headers.Add(header);
                current += direction * step;
            }
            return headers;
        }

        public async Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
        {
            var bodies = new List<BlockBody>();
            if (blockHashes == null) return bodies;

            int skippedAboveCursor = 0;
            foreach (var hash in blockHashes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (hash == null) continue;

                var exists = await _blockStore.ExistsAsync(hash);
                if (!exists)
                {
                    skippedAboveCursor++;
                    continue;
                }

                var txs = await _transactionStore.GetByBlockHashAsync(hash);
                bodies.Add(new BlockBody
                {
                    Transactions = txs ?? new List<ISignedTransaction>(),
                    Uncles = new List<BlockHeader>(),
                    Withdrawals = new List<Withdrawal>()
                });
            }

            if (skippedAboveCursor > 0)
            {
                _logger.LogDebug(
                    "skipped GetBlockBodies: {Skipped} of {Total} requested hashes not in local store",
                    skippedAboveCursor, blockHashes.Length);
            }
            return bodies;
        }

        public async Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
        {
            var result = new List<List<Receipt>>();
            if (blockHashes == null) return result;

            int skippedAboveCursor = 0;
            foreach (var hash in blockHashes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (hash == null) continue;

                var exists = await _blockStore.ExistsAsync(hash);
                if (!exists)
                {
                    skippedAboveCursor++;
                    continue;
                }

                var receipts = await _receiptStore.GetByBlockHashAsync(hash);
                result.Add(receipts?.ToList() ?? new List<Receipt>());
            }

            if (skippedAboveCursor > 0)
            {
                _logger.LogDebug(
                    "skipped GetReceipts: {Skipped} of {Total} requested hashes not in local store",
                    skippedAboveCursor, blockHashes.Length);
            }
            return result;
        }

        public async Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
        {
            var result = new List<ISignedTransaction>();
            if (_txPool == null || txHashes == null) return result;

            int runningBytes = 0;
            foreach (var hash in txHashes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (hash == null) continue;
                var tx = await _txPool.GetByHashAsync(hash);
                if (tx == null) continue;

                var raw = tx.GetRLPEncoded();
                if (runningBytes + raw.Length > PooledTxResponseSoftCapBytes && result.Count > 0) break;
                runningBytes += raw.Length;
                result.Add(tx);
            }
            return result;
        }
    }
}
