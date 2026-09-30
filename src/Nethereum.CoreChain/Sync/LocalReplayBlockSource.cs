using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Sync
{
    public sealed class LocalReplayBlockSource : IBlockSource
    {
        private readonly IList<BlockBundle> _bundles;

        public List<(ulong BlockNumber, BadBundleReason Reason)> BadBundleReports { get; } = new();

        public LocalReplayBlockSource(IList<BlockBundle> bundles)
        {
            _bundles = bundles;
        }

        public async IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var b in _bundles)
            {
                ct.ThrowIfCancellationRequested();
                if ((ulong)b.Header.BlockNumber < fromBlock) continue;
                yield return b;
                await Task.Yield();
            }
        }

        public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
            => Task.FromResult(BlockSourceHealth.Healthy);

        public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
        {
            BadBundleReports.Add((blockNumber, reason));
            return Task.CompletedTask;
        }

        public DivergenceSignal LastChainBreak => null;
    }
}
