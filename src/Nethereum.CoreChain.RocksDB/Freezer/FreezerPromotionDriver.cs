using System;
using System.Threading;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.RocksDB.Stores;

namespace Nethereum.CoreChain.RocksDB.Freezer
{
    internal sealed class FreezerPromotionDriver
    {
        private const long FreezerPromotionBatchBlocks = 8_192;

        private readonly FreezerAppendService _appendService;
        private readonly Nethereum.CoreChain.Freezer.FreezerPromotionService _freezerPromotionService;
        private readonly RocksDbHotBlockWindowSourceAdapter _freezerHotWindowAdapter;

        internal FreezerPromotionDriver(
            FreezerAppendService appendService,
            Nethereum.CoreChain.Freezer.FreezerPromotionService freezerPromotionService,
            RocksDbHotBlockWindowSourceAdapter freezerHotWindowAdapter)
        {
            _appendService = appendService;
            _freezerPromotionService = freezerPromotionService;
            _freezerHotWindowAdapter = freezerHotWindowAdapter;
        }

        internal static (Nethereum.CoreChain.Freezer.FreezerPromotionService Service, RocksDbHotBlockWindowSourceAdapter HotAdapter)
            Wire(
                RocksDbManager rocks, RocksDbHotBlockWindowStore hotWindow,
                Nethereum.Freezer.Freezer freezerAppend, Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet freezerCodecs)
        {
            var hotAdapter = new RocksDbHotBlockWindowSourceAdapter(rocks, hotWindow);
            var finality = new ImmutabilityDepthFinalitySource(
                () => (long)FreezerAppendService.ReadTipHeightStamp(rocks), FreezerAppendService.FullImmutabilityThreshold);
            var service = new Nethereum.CoreChain.Freezer.FreezerPromotionService(
                freezerAppend, freezerCodecs, hotAdapter, finality, new Nethereum.CoreChain.Freezer.NoOpRandomKeyIndexStore());
            return (service, hotAdapter);
        }

        internal void Drive() => Drive(default, () => false, () => false);

        internal void Drive(CancellationToken ct, Func<bool> yieldNow, Func<bool> recoveryWaiting)
        {
            if (_freezerPromotionService == null) return;

            Nethereum.CoreChain.Freezer.PromotionResult result;
            do
            {
                if (ct.IsCancellationRequested || yieldNow() || recoveryWaiting()) return;

                result = _appendService.CommitPendingThen(() => _freezerPromotionService.PromoteFinalizedBlocks(FreezerPromotionBatchBlocks));

                if (result.PromotedCount == 0) return;

                _freezerHotWindowAdapter.EvictAtOrBelow(result.NewFreezerItems - 1);
            }
            while (result.PromotedCount >= FreezerPromotionBatchBlocks);
        }
    }
}
