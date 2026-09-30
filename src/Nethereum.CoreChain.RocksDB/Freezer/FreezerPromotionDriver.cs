using System.Threading;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.RocksDB.Stores;

namespace Nethereum.CoreChain.RocksDB.Freezer
{
    internal sealed class FreezerPromotionDriver
    {
        private const long FreezerPromotionBatchBlocks = 8_192;

        private readonly FreezerAppendService _appendService;
        private readonly FreezerBackgroundIndexer _freezerIndexer;
        private readonly Nethereum.CoreChain.Freezer.FreezerPromotionService _freezerPromotionService;
        private readonly RocksDbHotBlockWindowSourceAdapter _freezerHotWindowAdapter;

        internal FreezerPromotionDriver(
            FreezerAppendService appendService,
            FreezerBackgroundIndexer freezerIndexer,
            Nethereum.CoreChain.Freezer.FreezerPromotionService freezerPromotionService,
            RocksDbHotBlockWindowSourceAdapter freezerHotWindowAdapter)
        {
            _appendService = appendService;
            _freezerIndexer = freezerIndexer;
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

        internal void Drive()
        {
            if (_freezerPromotionService == null) return;

            Nethereum.CoreChain.Freezer.PromotionResult result;
            do
            {
                result = _appendService.CommitPendingThen(() => _freezerPromotionService.PromoteFinalizedBlocks(FreezerPromotionBatchBlocks));

                if (result.PromotedCount == 0) return;

                if (!_freezerIndexer.IsRunning)
                {
                    _freezerIndexer.CatchUpByHash(result.NewFreezerItems, CancellationToken.None);
                    _freezerIndexer.RenderFilterMaps();
                }
                _freezerHotWindowAdapter.EvictAtOrBelow(result.NewFreezerItems - 1);
            }
            while (result.PromotedCount >= FreezerPromotionBatchBlocks);
        }
    }
}
