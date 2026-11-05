using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class BodyRepairer
    {
        private readonly IFetchRequestScheduler _scheduler;
        private readonly IBlockRootsProvider _rootsProvider;
        private readonly ILogger _logger;

        private const int ChunkSize = 64;
        private const ulong MaxRepairSpanPerCall = 1024;

        [Obsolete("Body holes are refilled by the tip band: TipBandBodyFollowService.RequeueAndWaitAsync owns the one body fetcher.")]
        public BodyRepairer(IFetchRequestScheduler scheduler, ILogger logger, IBlockRootsProvider rootsProvider = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            _rootsProvider = rootsProvider ?? new PatriciaBlockRootsProvider();
        }

        [Obsolete("Body holes are refilled by the tip band: TipBandBodyFollowService.RequeueAndWaitAsync owns the one body fetcher.")]
        public async Task<bool> RepairAsync(ulong fromBlock, ulong toBlock, IChainStoreBundle bundle, CancellationToken ct)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (toBlock < fromBlock) toBlock = fromBlock;
            if (toBlock - fromBlock + 1 > MaxRepairSpanPerCall) toBlock = fromBlock + MaxRepairSpanPerCall - 1;

            bool holeRepaired = false;
            for (ulong start = fromBlock; start <= toBlock; start += ChunkSize)
            {
                ct.ThrowIfCancellationRequested();
                ulong end = Math.Min(start + ChunkSize - 1, toBlock);

                var (headers, hashes) = await LoadChunkAsync(bundle, start, end).ConfigureAwait(false);
                if (headers.Count == 0) break;

                var realigned = await FetchValidatedBodiesAsync(headers, hashes, start, end, ct).ConfigureAwait(false);
                if (realigned == null) return holeRepaired;

                var paired = realigned.Count;
                for (int i = 0; i < paired; i++)
                    await PersistBodyAsync(bundle, hashes[i], headers[i].BlockNumber.ToBigInteger(), realigned[i])
                        .ConfigureAwait(false);

                _logger.LogInformation(
                    "body repair: persisted {Paired}/{Requested} bodies for [{Start}..{End}]",
                    paired, headers.Count, start, end);

                if (start == fromBlock && paired > 0) holeRepaired = true;
                if (paired < headers.Count) return holeRepaired;
            }
            return holeRepaired;
        }

        internal static Task PersistBodyAsync(
            IChainStoreBundle bundle, byte[] blockHash, BigInteger blockNumber, BlockBody body)
            => BlockPersistence.PersistBodyAsync(bundle, blockHash, blockNumber, body);

        private static async Task<(List<BlockHeader> Headers, List<byte[]> Hashes)> LoadChunkAsync(
            IChainStoreBundle bundle, ulong start, ulong end)
        {
            var headers = new List<BlockHeader>();
            var hashes = new List<byte[]>();
            for (ulong n = start; n <= end; n++)
            {
                var header = await bundle.Blocks.GetByNumberAsync(n).ConfigureAwait(false);
                var hash = header == null ? null : await bundle.Blocks.GetHashByNumberAsync(n).ConfigureAwait(false);
                if (header == null || hash == null) break;
                headers.Add(header);
                hashes.Add(hash);
            }
            return (headers, hashes);
        }

        private async Task<List<BlockBody>> FetchValidatedBodiesAsync(
            List<BlockHeader> headers, List<byte[]> hashes, ulong start, ulong end, CancellationToken ct)
        {
            BodyFetchResult result;
            try
            {
                result = await _scheduler.FetchBodiesAsync(hashes, excludePeers: null, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "body repair: fetch failed for [{Start}..{End}]", start, end);
                return null;
            }

            if (result?.Bodies == null || result.Bodies.Count == 0)
            {
                _logger.LogWarning("body repair: no bodies returned for [{Start}..{End}]", start, end);
                return null;
            }

            var realigned = BlockBatchValidator.RealignBodies(headers, result.Bodies, _rootsProvider, out _);
            var paired = realigned.Count;
            if (paired == 0 || !BlockBatchValidator.ValidateBodies(headers, realigned, paired, _rootsProvider))
            {
                _logger.LogWarning("body repair: bodies failed root validation for [{Start}..{End}]", start, end);
                return null;
            }
            return realigned;
        }
    }
}
