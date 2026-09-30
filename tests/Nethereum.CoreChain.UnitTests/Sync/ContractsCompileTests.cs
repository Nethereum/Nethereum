using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class ContractsCompileTests
    {
        private sealed class EmptyBlockSource : IBlockSource
        {
            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [EnumeratorCancellation] CancellationToken ct)
            {
                await Task.CompletedTask;
                yield break;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
                => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
                => Task.CompletedTask;

            public DivergenceSignal LastChainBreak => null;
        }

        private sealed class StubPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
                => ValidationAction.Continue;
        }

        [Fact]
        public void BlockBundle_RecordCarriesAllFields()
        {
            var bundle = new BlockBundle(
                Header: new BlockHeader(),
                Transactions: new List<ISignedTransaction>(),
                Uncles: new List<BlockHeader>(),
                Withdrawals: null,
                HeaderHash: new byte[32]);

            Assert.NotNull(bundle.Header);
            Assert.NotNull(bundle.Transactions);
            Assert.NotNull(bundle.Uncles);
            Assert.Null(bundle.Withdrawals);
            Assert.Equal(32, bundle.HeaderHash.Length);
        }

        [Fact]
        public async Task BlockSource_StubImplementsContract()
        {
            IBlockSource source = new EmptyBlockSource();
            var health = await source.GetHealthAsync(default);
            Assert.Equal(BlockSourceHealth.Healthy, health);
        }

        [Fact]
        public void Policy_StubReturnsContinue()
        {
            IValidationPolicy policy = new StubPolicy();
            Assert.False(policy.ShouldAnchorAt(0));
            Assert.Equal(ValidationAction.Continue,
                policy.OnVerdict(default, 0));
        }

        [Fact]
        public void FollowerOptions_RecordEquality()
        {
            var a = new FollowerOptions(1, 1000, 500);
            var b = new FollowerOptions(1, 1000, 500);
            Assert.Equal(a, b);
        }
    }
}
