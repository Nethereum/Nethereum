using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbPromotionService
    {
        private readonly RocksDbManager _core;
        private readonly RocksDbHotBlockWindowStore _hot;
        private readonly RocksDbBlockStore _historyBlocks;
        private readonly RocksDbTransactionStore _historyTransactions;
        private readonly RocksDbReceiptStore _historyReceipts;
        private readonly RocksDbBlockAccessListStore _historyBlockAccessLists;
        private readonly RocksDbChainMetadataStore _metadata;
        private readonly ulong _maxHistoryBlocks;

        public RocksDbPromotionService(
            RocksDbManager core, RocksDbHotBlockWindowStore hot,
            RocksDbBlockStore historyBlocks, RocksDbTransactionStore historyTransactions,
            RocksDbReceiptStore historyReceipts, RocksDbBlockAccessListStore historyBlockAccessLists,
            RocksDbChainMetadataStore metadata,
            ulong maxHistoryBlocks)
        {
            _historyBlockAccessLists = historyBlockAccessLists ?? throw new ArgumentNullException(nameof(historyBlockAccessLists));
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _historyBlocks = historyBlocks ?? throw new ArgumentNullException(nameof(historyBlocks));
            _historyTransactions = historyTransactions ?? throw new ArgumentNullException(nameof(historyTransactions));
            _historyReceipts = historyReceipts ?? throw new ArgumentNullException(nameof(historyReceipts));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _maxHistoryBlocks = maxHistoryBlocks;
        }

        public void PromoteDurableBlocks(ulong durableHead)
            => PromoteDurableBlocks(durableHead, default, () => false, () => false, null);

        internal void PromoteDurableBlocks(
            ulong durableHead, System.Threading.CancellationToken ct, Func<bool> yieldNow, Func<bool> recoveryWaiting,
            System.Threading.SemaphoreSlim cursorGate)
        {
            if (_maxHistoryBlocks == 0 || durableHead < _maxHistoryBlocks) return;
            var floor = durableHead - _maxHistoryBlocks;
            while (!ct.IsCancellationRequested && !yieldNow())
            {
                cursorGate?.Wait(ct);
                try
                {
                    var next = _metadata.GetPromotionCursor() + 1;
                    if (next > floor) break;
                    PromoteBlock(next);
                }
                finally
                {
                    cursorGate?.Release();
                }
                if (recoveryWaiting()) return;
            }
        }

        public void ReconcilePromotionOnBoot(ulong durableHead)
        {
            InitializePromotionCursorIfUnset(durableHead);
            PromoteDurableBlocks(durableHead);
            SweepHotAtOrBelowCursor();
        }

        private void InitializePromotionCursorIfUnset(ulong durableHead)
        {
            if (_metadata.HasPromotionCursor()) return;
            using var batch = _core.CreateWriteBatch();
            _metadata.AddPromotionCursorToBatch(batch, durableHead);
            _core.Write(batch);
        }

        private void SweepHotAtOrBelowCursor()
        {
            var cursor = _metadata.GetPromotionCursor();
            if (cursor == 0) return;
            using var batch = _core.CreateWriteBatch();
            _hot.RangeDeleteAtOrBelow(batch, cursor);
            _core.Write(batch);
        }

        private void PromoteBlock(ulong blockNumber)
        {
            var data = _hot.ReadForPromotion(blockNumber);
            if (data == null)
                throw new InvalidOperationException(
                    $"Promotion could not read block {blockNumber} from the hot window; refusing to advance " +
                    "the promotion cursor past a block that is in neither store.");
            AppendHistoryDurable(blockNumber, data);
            AdvanceCursorAndEvictHotAtomically(blockNumber);
        }

        private void AppendHistoryDurable(ulong blockNumber, HotBlockPromotionData data)
        {
            using var batch = _core.CreateWriteBatch();
            _historyBlocks.StageBlockMetaInto(batch, data.Header, data.BlockHash, new BlockMeta
            {
                BlockHash = data.BlockHash,
                TxCount = data.Transactions?.Count ?? 0,
                Uncles = data.Meta.Uncles,
                Withdrawals = data.Meta.Withdrawals,
                Bloom = data.Meta.Bloom,
            });
            if (data.Transactions != null && data.Transactions.Count > 0)
                _historyTransactions.StageManyInto(batch, data.BlockHash, blockNumber, data.Transactions);
            if (data.Receipts != null && data.Receipts.Count > 0)
                _historyReceipts.StageManyInto(batch, data.BlockHash, blockNumber, ToSaveItems(data.Receipts));
            _historyBlockAccessLists.StageInto(batch, blockNumber, data.BlockAccessList);
            _core.Write(batch);
        }

        private void AdvanceCursorAndEvictHotAtomically(ulong blockNumber)
        {
            using var batch = _core.CreateWriteBatch();
            _metadata.AddPromotionCursorToBatch(batch, blockNumber);
            _hot.RangeDeleteAtOrBelow(batch, blockNumber);
            _core.Write(batch);
        }

        private static IReadOnlyList<ReceiptSaveItem> ToSaveItems(List<ReceiptInfo> receipts)
        {
            var items = new ReceiptSaveItem[receipts.Count];
            for (int i = 0; i < receipts.Count; i++)
            {
                var r = receipts[i];
                items[i] = new ReceiptSaveItem(r.Receipt, r.TxHash, r.TransactionIndex, r.GasUsed, r.ContractAddress, r.EffectiveGasPrice);
            }
            return items;
        }
    }
}
