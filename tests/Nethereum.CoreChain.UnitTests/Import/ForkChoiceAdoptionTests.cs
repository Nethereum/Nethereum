using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Import
{
    public class ForkChoiceAdoptionTests
    {
        private static readonly BigInteger OneGwei = 1_000_000_000;

        [Fact]
        public async Task Given_AForkChoiceOverZeroDifficultyBranches_When_ASiblingIsImported_Then_ItBecomesTheHeadAndTheResultCarriesTheReorganisation()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync(adoptByDifficulty: true);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 2);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2);
            foreach (var block in a) await node.ImportAsync(block);

            var first = await node.ImportAsync(b[0]);
            var second = await node.ImportAsync(b[1]);

            Assert.Equal(BlockImportDisposition.ExtendedCanonical, first.Disposition);
            Assert.True(first.RootMatches);
            Assert.Equal(0UL, first.Reorganisation.CommonAncestor);
            Assert.Equal(2, first.Reorganisation.DroppedBlockHashes.Count);
            Assert.Equal(BlockImportDisposition.ExtendedCanonical, second.Disposition);
            Assert.Null(second.Reorganisation);
            Assert.Equal(b[1].Hash, await node.HeadHashAsync());
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(SideChainHarness.RecipientA));
            Assert.Equal(2 * OneGwei, await node.BalanceOfAsync(SideChainHarness.RecipientB));
        }

        [Fact]
        public async Task Given_AnAdoptedSibling_When_ItsChildHasAForgedStateRoot_Then_TheChildIsRefusedAndTheHeadStaysOnTheAdoptedBlock()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync(adoptByDifficulty: true);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 1);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2);
            await node.ImportAsync(a[0]);
            var forgedHeader = BlockPipelineHarness.CloneHeader(b[1].Header);
            forgedHeader.StateRoot = new byte[32];
            forgedHeader.StateRoot[0] = 9;

            var adopted = await node.Importer.ImportAsync(
                b[0].Header, b[0].Transactions, null, b[0].Withdrawals, System.Threading.CancellationToken.None);
            var rejected = await node.Importer.ImportAsync(
                forgedHeader, b[1].Transactions, null, b[1].Withdrawals, System.Threading.CancellationToken.None);

            Assert.Equal(BlockImportDisposition.ExtendedCanonical, adopted.Disposition);
            Assert.False(rejected.RootMatches);
            Assert.Equal(BlockImportDisposition.Refused, rejected.Disposition);
            Assert.Equal(b[0].Hash, await node.HeadHashAsync());
        }
    }
}
