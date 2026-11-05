using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class StreamFollowerGapRestartTests
    {
        private sealed class GappedSource : IBlockSource
        {
            public int Passes;

            public DivergenceSignal LastChainBreak => null;

            public async IAsyncEnumerable<BlockBundle> StreamAsync(ulong fromBlock, [EnumeratorCancellation] CancellationToken ct)
            {
                Interlocked.Increment(ref Passes);
                yield return new BlockBundle(
                    new BlockHeader { BlockNumber = (long)fromBlock + 40, ParentHash = new byte[32] },
                    new List<ISignedTransaction>(), new List<BlockHeader>(), null, new byte[32]);
                await Task.CompletedTask;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class UnusedExecutor : IBlockExecutor
        {
            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
                IList<Withdrawal> withdrawals, CancellationToken ct)
                => throw new InvalidOperationException("a gapped block must not execute");
        }

        private sealed class AcceptEverythingPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        [Fact]
        public async Task Given_TheSourceKeepsHandingBlocksAheadOfOurHead_When_TheStreamFollowerRestartsOnTheGap_Then_RestartsAreBackedOffInsteadOfHot()
        {
            var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            var genesisHash = await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, HardforkName.Prague, 30_000_000);
            bundle.Metadata.Commit(0, genesisHash);
            var source = new GappedSource();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1.2));

            var result = await new FollowerService(null, null, null).RunAsync(
                source,
                () => bundle,
                _ => new UnusedExecutor(),
                new AcceptEverythingPolicy(),
                null,
                new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0, EndBlock: 1000)
                { ChainWriteGate = new AsyncGate() },
                cts.Token);

            Assert.Equal(FollowerExitReason.Cancelled, result.ExitReason);
            Assert.InRange(source.Passes, 1, 8);
        }
    }
}
