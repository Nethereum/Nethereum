using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Import
{
    public class SideChainImportTests
    {
        private static readonly BigInteger OneGwei = 1_000_000_000;

        [Fact]
        public async Task Given_ABlockWhoseParentIsNotTheHead_When_Imported_Then_ItIsStoredAsSideAndTheLiveStateIsUntouched()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var b1 = await producerB.ProduceAsync(SideChainHarness.RecipientB);
            await node.ImportAsync(a1);

            var result = await node.ImportAsync(b1);

            Assert.Equal(BlockImportDisposition.StoredAsSide, result.Disposition);
            Assert.False(result.RootMatches);
            Assert.False(result.StateRootMismatch);
            Assert.Empty(result.FailedChecks);
            Assert.True(node.SideChain.SideBlocks.Contains(b1.Hash));
            Assert.Equal(a1.Hash, await node.HeadHashAsync());
            Assert.Equal(OneGwei, await node.BalanceOfAsync(SideChainHarness.RecipientA));
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(SideChainHarness.RecipientB));
        }

        [Fact]
        public async Task Given_ASideBlockWithABadTransactionsRoot_When_Imported_Then_ItIsRefusedWithoutExecution()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var b1 = await producerB.ProduceAsync(SideChainHarness.RecipientB);
            await node.ImportAsync(a1);
            var forged = BlockPipelineHarness.CloneHeader(b1.Header);
            forged.TransactionsHash = new byte[32];
            forged.TransactionsHash[0] = 1;

            var result = await node.Importer.ImportAsync(
                forged, b1.Transactions, uncles: null, b1.Withdrawals, CancellationToken.None);

            Assert.Equal(BlockImportDisposition.Refused, result.Disposition);
            Assert.True(result.TransactionsRootMismatch, result.DescribeRejection());
            Assert.False(result.StateRootMismatch);
            Assert.Null(result.ComputedStateRoot);
            Assert.Equal(a1.Hash, await node.HeadHashAsync());
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(SideChainHarness.RecipientB));
        }

        [Fact]
        public async Task Given_ASideBlockWhoseParentIsUnknown_When_Imported_Then_TheResultIsUnknownParent()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var orphan = BlockPipelineHarness.CloneHeader(a1.Header);
            orphan.ParentHash = new byte[32];
            orphan.ParentHash[31] = 7;

            var result = await node.Importer.ImportAsync(
                orphan, a1.Transactions, uncles: null, a1.Withdrawals, CancellationToken.None);

            Assert.Equal(BlockImportDisposition.UnknownParent, result.Disposition);
            Assert.True(result.UnknownParent);
            Assert.False(result.RootMatches);
        }

        [Fact]
        public async Task Given_AStoredSideBlock_When_ImportedAgain_Then_TheDispositionIsAlreadySide()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var b1 = await producerB.ProduceAsync(SideChainHarness.RecipientB);
            await node.ImportAsync(a1);
            await node.ImportAsync(b1);

            var again = await node.ImportAsync(b1);

            Assert.Equal(BlockImportDisposition.AlreadySide, again.Disposition);
            Assert.Equal(b1.Hash, again.BlockHash);
            Assert.Equal(a1.Hash, await node.HeadHashAsync());
        }

        [Fact]
        public async Task Given_ACanonicalBlock_When_ImportedAgain_Then_TheDispositionIsAlreadyCanonicalAndRootMatches()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var first = await node.ImportAsync(a1);
            Assert.True(first.RootMatches, first.DescribeRejection());
            Assert.Equal(BlockImportDisposition.ExtendedCanonical, first.Disposition);

            var again = await node.ImportAsync(a1);

            Assert.Equal(BlockImportDisposition.AlreadyCanonical, again.Disposition);
            Assert.True(again.RootMatches, again.DescribeRejection());
            Assert.Equal(OneGwei, await node.BalanceOfAsync(SideChainHarness.RecipientA));
        }

        [Fact]
        public async Task Given_NoSideStoreConfigured_When_ANonExtendingBlockArrives_Then_ItIsRefusedAsParentNotHead()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync(withSideChain: false);
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var b1 = await producerB.ProduceAsync(SideChainHarness.RecipientB);
            await node.ImportAsync(a1);

            var result = await node.ImportAsync(b1);

            Assert.Equal(BlockImportDisposition.Refused, result.Disposition);
            Assert.True(result.ParentNotHead, result.DescribeRejection());
            Assert.Contains("parentNotHead", result.FailedChecks);
            Assert.Equal(a1.Hash, await node.HeadHashAsync());
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(SideChainHarness.RecipientB));
        }
    }
}
