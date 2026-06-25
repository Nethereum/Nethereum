using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    /// <summary>
    /// Loads Hive testdata chain.rlp into <see cref="BlockBundle"/>s ordered by
    /// block number. Used by integration tests against the CoreChain follower
    /// architecture (IBlockSource / IBlockExecutor / FollowerService).
    /// Decoded bundles are sourced from <see cref="HiveTestdataFixture.Chain"/>
    /// which caches a single decode for the lifetime of the test run.
    /// </summary>
    public sealed class HiveChainRlpBlockSource : IBlockSource
    {
        private readonly IReadOnlyList<BlockBundle> _bundles;

        public List<(ulong BlockNumber, BadBundleReason Reason)> BadBundleReports { get; } = new();

        public DivergenceSignal LastChainBreak { get; set; }

        public HiveChainRlpBlockSource(IReadOnlyList<BlockBundle> bundles)
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
    }
}
