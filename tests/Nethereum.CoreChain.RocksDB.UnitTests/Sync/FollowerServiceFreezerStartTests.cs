using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class FollowerServiceFreezerStartTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"followerfreezer_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        private sealed class UnusedExecutor : IBlockExecutor
        {
            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
                IList<Withdrawal> withdrawals, CancellationToken ct)
                => throw new InvalidOperationException("no block is replayed");
        }

        private sealed class ContinuePolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        [Fact]
        public async Task Given_AFreezerEligibleStoreAndNoHost_When_FollowerServiceRunsWithoutMainnetHost_Then_AgedBlocksAreFrozen()
        {
            using var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: true));
            foreach (var block in MakeChainedBlocks(0, 6))
                await WriteToHotAsync(bundle, block);
            StampTipHeight(bundle.Rocks, ImmutabilityThreshold + 3);

            var result = await new FollowerService().RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                () => bundle,
                _ => new UnusedExecutor(),
                new ContinuePolicy(),
                null,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            await WaitUntilAsync(() => bundle.FreezerHead == 4, TimeSpan.FromSeconds(30),
                $"the follower did not start the freezer job: FreezerHead={bundle.FreezerHead}");
        }
    }
}
