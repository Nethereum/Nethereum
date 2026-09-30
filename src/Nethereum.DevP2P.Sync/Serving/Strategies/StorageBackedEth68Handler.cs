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

namespace Nethereum.DevP2P.Sync.Serving.Strategies
{
    public class StorageBackedEth68Handler : IEth68RequestHandler
    {
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly IWithdrawalStore _withdrawalStore;
        private readonly ITxPool _txPool;
        private readonly IBlockAccessListStore _blockAccessListStore;
        private readonly ILogger<StorageBackedEth68Handler> _logger;

        private const int ResponseSoftCapBytes = 2 * 1024 * 1024;

        public StorageBackedEth68Handler(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            IWithdrawalStore withdrawalStore = null,
            ITxPool txPool = null,
            ILogger<StorageBackedEth68Handler> logger = null)
        {
            _blockStore = blockStore;
            _transactionStore = transactionStore;
            _receiptStore = receiptStore;
            _withdrawalStore = withdrawalStore;
            _txPool = txPool;
            _logger = logger ?? NullLogger<StorageBackedEth68Handler>.Instance;
        }

        public StorageBackedEth68Handler(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            IBlockAccessListStore blockAccessListStore,
            IWithdrawalStore withdrawalStore,
            ITxPool txPool,
            ILogger<StorageBackedEth68Handler> logger)
            : this(blockStore, transactionStore, receiptStore, withdrawalStore, txPool, logger)
        {
            _blockAccessListStore = blockAccessListStore;
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
            int runningBytes = 0;
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
                var withdrawals = _withdrawalStore != null
                    ? await _withdrawalStore.GetByBlockHashAsync(hash)
                    : null;
                var body = new BlockBody
                {
                    Transactions = txs ?? new List<ISignedTransaction>(),
                    Uncles = new List<BlockHeader>(),
                    Withdrawals = withdrawals?.ToList() ?? new List<Withdrawal>()
                };

                var itemBytes = BlockBodiesMessageEncoder.EncodeBody(body).Length;
                if (runningBytes + itemBytes > ResponseSoftCapBytes && bodies.Count > 0) break;
                runningBytes += itemBytes;
                bodies.Add(body);
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
            int runningBytes = 0;
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
                var blockReceipts = receipts?.ToList() ?? new List<Receipt>();

                var itemBytes = ReceiptsMessageEncoder.EncodeBlockReceipts(blockReceipts).Length;
                if (runningBytes + itemBytes > ResponseSoftCapBytes && result.Count > 0) break;
                runningBytes += itemBytes;
                result.Add(blockReceipts);
            }

            if (skippedAboveCursor > 0)
            {
                _logger.LogDebug(
                    "skipped GetReceipts: {Skipped} of {Total} requested hashes not in local store",
                    skippedAboveCursor, blockHashes.Length);
            }
            return result;
        }

        public async Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default)
        {
            var result = new List<List<Receipt>>();
            if (blockHashes == null) return new Receipts70Result(result, false);

            long runningBytes = 0;
            bool lastBlockIncomplete = false;

            for (int b = 0; b < blockHashes.Length; b++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hash = blockHashes[b];
                if (hash == null) continue;

                var exists = await _blockStore.ExistsAsync(hash);
                if (!exists) continue;

                var receipts = await _receiptStore.GetByBlockHashAsync(hash);
                var allBlockReceipts = receipts?.ToList() ?? new List<Receipt>();

                var startIndex = b == 0
                    ? (int)System.Math.Min(firstBlockReceiptIndex, (ulong)allBlockReceipts.Count)
                    : 0;

                var included = new List<Receipt>();
                for (int i = startIndex; i < allBlockReceipts.Count; i++)
                {
                    var r = allBlockReceipts[i];
                    var itemBytes = ReceiptsMessageEth69Encoder.EncodeSingleReceipt(r).Length;
                    var haveAnyReceiptSoFar = result.Count > 0 || included.Count > 0;
                    if (runningBytes + itemBytes > (long)sizeCap && haveAnyReceiptSoFar)
                    {
                        lastBlockIncomplete = true;
                        break;
                    }
                    runningBytes += itemBytes;
                    included.Add(r);
                }

                result.Add(included);
                if (lastBlockIncomplete) break;
            }

            return new Receipts70Result(result, lastBlockIncomplete);
        }

        /// <summary>
        /// eth/71 GetBlockAccessLists (EIP-8159) serve-empty contract: this
        /// node negotiates eth/71 and serves the verbatim BAL RLP CoreChain
        /// retains past block execution in
        /// <see cref="Nethereum.CoreChain.Storage.IBlockAccessListStore"/>
        /// (written by <c>BlockImporter</c>, reclaimed on reorg by
        /// <c>RewindCoordinator</c>). Constructed without one, it serves nothing
        /// rather than refusing or leaving the request undispatched.
        ///
        /// <para>EIP-8159 §BlockAccessLists (0x13): <i>"The RLP empty string
        /// (<c>0x80</c>) is returned for blocks where the BAL is unavailable."</i>
        /// So a hash the store cannot answer yields an EMPTY ENTRY THAT KEEPS ITS
        /// POSITION, and the walk continues — entry <c>i</c> answers
        /// <paramref name="blockHashes"/><c>[i]</c> for the whole response.
        /// <see cref="Nethereum.Model.P2P.BlockAccessListWireEntries"/> turns that
        /// empty entry into the byte the EIP names.</para>
        ///
        /// <para>NOT geth's stop-at-gap, which an earlier version of this comment
        /// instructed. geth serves a prefix and stops at the first hash it cannot
        /// answer; the EIP does not, and geth is a reference rather than the
        /// specification. Serving a prefix while the requester indexes by position
        /// hands back a different block's access list, which then fails against
        /// <c>header.BlockAccessListHash</c> for a block that was served correctly.</para>
        ///
        /// <para>Nothing here decodes a BAL blob; only the requesting client does.</para>
        /// </summary>
        public async Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
        {
            var served = new List<byte[]>();
            if (_blockAccessListStore == null || blockHashes == null) return served;

            var runningBytes = 0;
            foreach (var blockHash in blockHashes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var balRlp = blockHash == null || blockHash.Length != 32
                    ? null
                    : await _blockAccessListStore.GetByBlockHashAsync(blockHash);
                var entry = balRlp ?? System.Array.Empty<byte>();

                if (runningBytes + entry.Length > ResponseSoftCapBytes && served.Count > 0) break;
                runningBytes += entry.Length;
                served.Add(entry);
            }

            return served;
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

                var raw = tx is Transaction4844 blobTx
                    ? blobTx.GetRLPEncodedWithSidecar()
                    : tx.GetRLPEncoded();
                if (runningBytes + raw.Length > ResponseSoftCapBytes && result.Count > 0) break;
                runningBytes += raw.Length;
                result.Add(tx);
            }
            return result;
        }
    }
}
