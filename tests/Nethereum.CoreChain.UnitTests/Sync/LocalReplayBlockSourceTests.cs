using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class LocalReplayBlockSourceTests
    {
        private static BlockBundle MakeBundle(ulong blockNumber)
            => new BlockBundle(
                Header: new BlockHeader { BlockNumber = blockNumber },
                Transactions: new List<ISignedTransaction>(),
                Uncles: new List<BlockHeader>(),
                Withdrawals: null,
                HeaderHash: new byte[32]);

        [Fact]
        public async Task StreamAsync_YieldsAllBundles_InOrder()
        {
            var source = new LocalReplayBlockSource(new List<BlockBundle>
            {
                MakeBundle(1), MakeBundle(2), MakeBundle(3),
            });

            var seen = new List<ulong>();
            await foreach (var b in source.StreamAsync(0, default))
            {
                seen.Add((ulong)b.Header.BlockNumber);
            }

            Assert.Equal(new ulong[] { 1, 2, 3 }, seen);
        }

        [Fact]
        public async Task StreamAsync_StartFromMid_SkipsEarlier()
        {
            var source = new LocalReplayBlockSource(new List<BlockBundle>
            {
                MakeBundle(1), MakeBundle(2), MakeBundle(3),
            });

            var seen = new List<ulong>();
            await foreach (var b in source.StreamAsync(2, default))
            {
                seen.Add((ulong)b.Header.BlockNumber);
            }

            Assert.Equal(new ulong[] { 2, 3 }, seen);
        }

        [Fact]
        public async Task StreamAsync_RespectsCancellation()
        {
            var source = new LocalReplayBlockSource(new List<BlockBundle>
            {
                MakeBundle(1), MakeBundle(2),
            });
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<System.OperationCanceledException>(async () =>
            {
                await foreach (var b in source.StreamAsync(0, cts.Token))
                {
                }
            });
        }

        [Fact]
        public async Task ReportBadBundleAsync_RecordsForInspection()
        {
            var source = new LocalReplayBlockSource(new List<BlockBundle>());

            await source.ReportBadBundleAsync(42, BadBundleReason.StateRootMismatch, default);

            Assert.Single(source.BadBundleReports);
            Assert.Equal((42UL, BadBundleReason.StateRootMismatch), source.BadBundleReports[0]);
        }
    }
}
