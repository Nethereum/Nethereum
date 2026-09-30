using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Util;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.Freezer
{
    public sealed class FreezerPromotionService
    {
        private readonly FreezerCore _freezer;
        private readonly FreezerCodecSet _codecs;
        private readonly IHotBlockWindowSource _hot;
        private readonly IFinalitySource _finality;
        private readonly IRandomKeyIndexStore _indexes;

        private long? _lastObservedFinalized;

        public FreezerPromotionService(
            FreezerCore freezer,
            FreezerCodecSet codecs,
            IHotBlockWindowSource hot,
            IFinalitySource finality,
            IRandomKeyIndexStore indexes)
        {
            _freezer = freezer ?? throw new ArgumentNullException(nameof(freezer));
            _codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _finality = finality ?? throw new ArgumentNullException(nameof(finality));
            _indexes = indexes ?? throw new ArgumentNullException(nameof(indexes));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerPromotionService.PromoteFinalizedBlocks — finality-gated freeze")]
        public PromotionResult PromoteFinalizedBlocks(long maxBlocksPerCall = long.MaxValue)
        {
            var upperBound = ClampToBatch(ResolvePromotionUpperBound(), maxBlocksPerCall);
            if (upperBound < _freezer.Items)
                return new PromotionResult(0, _freezer.Items);

            var batch = _freezer.BeginBatch();
            List<(long Number, HotBlock Block)> promoted;
            long newItems;

            try
            {
                promoted = AppendRange(batch, _freezer.Items, upperBound);
                newItems = batch.Commit();
            }
            catch
            {
                batch.Reset();
                throw;
            }

            IndexPromoted(promoted);
            return new PromotionResult(promoted.Count, newItems);
        }

        private long ClampToBatch(long upperBound, long maxBlocksPerCall)
        {
            if (maxBlocksPerCall <= 0 || maxBlocksPerCall == long.MaxValue) return upperBound;
            var batchCeiling = _freezer.Items + maxBlocksPerCall - 1;
            return Math.Min(upperBound, batchCeiling);
        }

        private long ResolvePromotionUpperBound()
        {
            var finalized = _finality.FinalizedBlockNumber;
            EnforceFinalityMonotonic(finalized);
            return Math.Min(finalized, _hot.HotTipNumber);
        }

        private void EnforceFinalityMonotonic(long finalized)
        {
            if (_lastObservedFinalized.HasValue && finalized < _lastObservedFinalized.Value)
                throw new FreezerConsistencyException(
                    $"finality regressed from {_lastObservedFinalized.Value} to {finalized}; " +
                    "the freezer cannot un-freeze already-promoted blocks");

            _lastObservedFinalized = finalized;
        }

        private List<(long Number, HotBlock Block)> AppendRange(FreezerBatch batch, long from, long to)
        {
            var previousHash = from > 0 ? ReadFrozenHash(from - 1) : null;
            var promoted = new List<(long Number, HotBlock Block)>();

            for (var number = from; number <= to; number++)
            {
                var hotBlock = _hot.ReadHotBlock(number);
                RequireChainedToParent(number, hotBlock, previousHash);

                AppendCluster(batch, number, hotBlock);
                promoted.Add((number, hotBlock));

                previousHash = hotBlock.BlockHash;
            }

            return promoted;
        }

        private byte[] ReadFrozenHash(long number) => _codecs.Hashes.Decode(_freezer.ReadCluster(number).Hash);

        private static void RequireChainedToParent(long number, HotBlock hotBlock, byte[] previousHash)
        {
            if (previousHash == null)
                return;

            if (!previousHash.SequenceEqual(hotBlock.Header.ParentHash ?? Array.Empty<byte>()))
                throw new FreezerConsistencyException(
                    $"hot block {number}'s parent hash does not match the freezer head; " +
                    "promotion requires contiguous history");
        }

        private void AppendCluster(FreezerBatch batch, long number, HotBlock hotBlock)
        {
            var cluster = new FrozenBlockCluster(
                header: _codecs.Headers.Encode(hotBlock.Header),
                hash: _codecs.Hashes.Encode(hotBlock.BlockHash),
                body: _codecs.Bodies.Encode(hotBlock.Body),
                receipts: _codecs.Receipts.Encode(ToStoredReceipts(hotBlock.Receipts)),
                bal: _codecs.Bals.Encode(hotBlock.BalRlp));

            batch.AppendCluster(number, cluster);
        }

        private static List<ReceiptForStorage> ToStoredReceipts(IReadOnlyList<Receipt> receipts) =>
            receipts.Select(r => new ReceiptForStorage(r.PostStateOrStatus, r.CumulativeGasUsed.ToBigInteger(), r.Logs)).ToList();

        private void IndexPromoted(List<(long Number, HotBlock Block)> promoted)
        {
            foreach (var (number, hotBlock) in promoted)
                IndexCluster(number, hotBlock);
        }

        private void IndexCluster(long number, HotBlock hotBlock)
        {
            _indexes.PutBlockHash(hotBlock.BlockHash, number);

            var txs = hotBlock.Body.Txs;
            for (var i = 0; i < txs.Count; i++)
                _indexes.PutTxLocation(txs[i].Hash, number, i);
        }
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "PromotionResult — promoted count and new freezer head")]
    public readonly struct PromotionResult
    {
        public long PromotedCount { get; }
        public long NewFreezerItems { get; }

        public PromotionResult(long promotedCount, long newFreezerItems)
        {
            PromotedCount = promotedCount;
            NewFreezerItems = newFreezerItems;
        }
    }
}
