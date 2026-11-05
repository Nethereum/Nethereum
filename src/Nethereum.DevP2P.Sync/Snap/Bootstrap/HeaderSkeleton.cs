using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class HeaderSkeleton
    {
        public static BackwardBlockWalker CreateWalker(IFetchRequestScheduler scheduler, IChainStoreBundle bundle, ILogger logger)
            => new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeadersOnly = true },
                logger);

        public static Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> StoredHashLookup(IChainStoreBundle bundle)
            => async (n, c) =>
            {
                var h = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)).ConfigureAwait(false);
                return (h, h != null);
            };

        public static void OpenTip(IChainStoreBundle bundle, ulong tip)
            => bundle.Metadata.SaveHeaderSyncState(
                HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), tip));

        public static void OpenTipAndRecordDescent(IChainStoreBundle bundle, ulong tip, ulong bottom)
            => bundle.Metadata.SaveHeaderSyncState(
                HeaderSubchains.RecordDescent(
                    HeaderSubchains.OpenTip(bundle.Metadata.GetHeaderSyncState(), tip),
                    tip, bottom));

        public static async Task<WalkResult> WalkAndRecordAsync(
            BackwardBlockWalker walker, IChainStoreBundle bundle, ulong tip,
            ulong fromBlock, byte[] fromHash, ulong toBlock,
            Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> lookup, CancellationToken ct)
        {
            OpenTip(bundle, tip);
            var walk = await walker.WalkAsync(fromBlock, fromHash, toBlock, lookup, ct).ConfigureAwait(false);
            RecordDescent(bundle, tip, walk.SkeletonBottomBlock);
            return walk;
        }

        public static void RecordDescent(IChainStoreBundle bundle, ulong tip, ulong bottom)
            => bundle.Metadata.SaveHeaderSyncState(
                HeaderSubchains.RecordDescent(bundle.Metadata.GetHeaderSyncState(), tip, bottom));
    }
}
