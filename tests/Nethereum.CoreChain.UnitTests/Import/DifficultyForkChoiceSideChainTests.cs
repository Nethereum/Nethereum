using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Import
{
    public class DifficultyForkChoiceSideChainTests
    {
        private static async Task<ProducedBlock> SiblingWithHashAboveAsync(byte[] reference)
        {
            for (var offset = 12; offset < 60; offset++)
            {
                using var producer = await SideChainHarness.CreateAsync(withSideChain: false);
                var candidate = await producer.ProduceAsync(SideChainHarness.RecipientB, secondsAfterParent: offset);
                if (ByteArrayComparer.Current.Compare(candidate.Hash, reference) > 0) return candidate;
            }
            throw new Xunit.Sdk.XunitException("no sibling with a higher hash was found");
        }

        [Fact]
        public async Task Given_TwoZeroDifficultyBranches_When_Compared_Then_TheIncomingBranchIsAdopted()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var b1 = await SiblingWithHashAboveAsync(a1.Hash);
            await node.ImportAsync(a1);
            await node.ImportAsync(b1);
            var forkChoice = new DifficultyForkChoice(node.SideChain.Blocks);

            var verdict = await forkChoice.ShouldAdoptAsync(b1.Header, b1.Hash, CancellationToken.None);

            Assert.Equal(ForkChoiceOutcome.AdoptIncoming, verdict.Outcome);
            Assert.Equal(0UL, verdict.CommonAncestor);
        }

        [Fact]
        public async Task Given_ASideBranchHeldOnlyInTheSideStore_When_Compared_Then_TheWalkReachesTheCommonAncestor()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var a1 = await producerA.ProduceAsync(SideChainHarness.RecipientA);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2);
            await node.ImportAsync(a1);
            foreach (var block in b) await node.ImportAsync(block);

            var throughSideStore = await new DifficultyForkChoice(node.SideChain.Blocks)
                .ShouldAdoptAsync(b[1].Header, b[1].Hash, CancellationToken.None);
            var canonicalOnly = await new DifficultyForkChoice(node.Node.Blocks)
                .ShouldAdoptAsync(b[1].Header, b[1].Hash, CancellationToken.None);

            Assert.NotEqual(ForkChoiceOutcome.Undecidable, throughSideStore.Outcome);
            Assert.Equal(ForkChoiceOutcome.Undecidable, canonicalOnly.Outcome);
        }
    }
}
