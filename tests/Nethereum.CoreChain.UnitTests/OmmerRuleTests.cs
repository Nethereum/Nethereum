using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class OmmerRuleTests
    {
        private static async Task<BlockImporterResult> ImportAsync(
            OmmerRuleHarness harness, params BlockHeader[] uncles)
        {
            var list = new List<BlockHeader>(uncles);
            return await harness.ImportAsync(harness.Child(list), list);
        }

        private static void AssertRefusedForUncles(BlockImporterResult result)
        {
            Assert.Equal(new[] { BlockValidityCheck.Uncles }, result.FailedValidityChecks);
            Assert.Null(result.BlockHash);
        }

        private static void AssertNoUncleRefusal(BlockImporterResult result)
        {
            Assert.DoesNotContain(BlockValidityCheck.Uncles, result.FailedValidityChecks);
            Assert.DoesNotContain(BlockValidityCheck.UnclesHash, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_OneRecentUncle_When_Imported_Then_NoUncleRefusal()
        {
            var harness = await OmmerRuleHarness.CreateAsync();

            AssertNoUncleRefusal(await ImportAsync(harness, harness.UncleOfAncestorAt(parentDepth: 2)));
        }

        [Fact]
        public async Task Given_ThreeUncles_When_Imported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync();

            AssertRefusedForUncles(await ImportAsync(
                harness,
                harness.UncleOfAncestorAt(2, "a"),
                harness.UncleOfAncestorAt(3, "a"),
                harness.UncleOfAncestorAt(4, "a")));
        }

        [Fact]
        public async Task Given_TwoUnclesOfDifferentParents_When_Imported_Then_NoUncleRefusal()
        {
            var harness = await OmmerRuleHarness.CreateAsync();

            AssertNoUncleRefusal(await ImportAsync(
                harness, harness.UncleOfAncestorAt(2, "a"), harness.UncleOfAncestorAt(3, "a")));
        }

        [Fact]
        public async Task Given_TheSameUncleTwice_When_Imported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync();
            var uncle = harness.UncleOfAncestorAt(2);

            AssertRefusedForUncles(await ImportAsync(harness, uncle, uncle));
        }

        [Fact]
        public async Task Given_AnUncleAnAncestorAlreadyIncluded_When_Imported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync();
            var uncle = harness.UncleOfAncestorAt(3);
            await harness.UncleStore.SaveAsync(harness.AncestorHashAt(2), new List<BlockHeader> { uncle });
            harness.AncestorAt(2).UnclesHash = OmmerRuleHarness.HashOfUncles(new List<BlockHeader> { uncle });

            AssertRefusedForUncles(await ImportAsync(harness, uncle));
        }

        [Fact]
        public async Task Given_ANodeWithoutAnUncleStore_When_AnAncestorCarriesUnclesAndAnUncleOfAnOlderAncestorIsImported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync(withUncleStore: false);
            harness.AncestorAt(2).UnclesHash = new byte[32];

            AssertRefusedForUncles(await ImportAsync(harness, harness.UncleOfAncestorAt(4)));
        }

        [Fact]
        public async Task Given_AnUncleThatIsAnAncestor_When_Imported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync();

            AssertRefusedForUncles(await ImportAsync(harness, harness.AncestorAt(3)));
        }

        [Fact]
        public async Task Given_AnUncleWhoseParentIsTheBlocksParent_When_Imported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync();

            AssertRefusedForUncles(await ImportAsync(harness, harness.UncleOfAncestorAt(1)));
        }

        [Fact]
        public async Task Given_AnUncleWhoseParentIs7Deep_When_Imported_Then_NoUncleRefusal()
        {
            var harness = await OmmerRuleHarness.CreateAsync();

            AssertNoUncleRefusal(await ImportAsync(harness, harness.UncleOfAncestorAt(7)));
        }

        [Fact]
        public async Task Given_AnUncleWhoseParentIs8Deep_When_Imported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync();

            AssertRefusedForUncles(await ImportAsync(harness, harness.UncleOfAncestorAt(8)));
        }

        [Fact]
        public async Task Given_AnUncleWithATimestampNotAfterItsParent_When_Imported_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await OmmerRuleHarness.CreateAsync();
            var uncle = harness.UncleOfAncestorAt(2);
            uncle.Timestamp = harness.AncestorAt(2).Timestamp;

            AssertRefusedForUncles(await ImportAsync(harness, uncle));
        }

        [Fact]
        public async Task Given_AHeaderWhoseUnclesHashIgnoresTheBodyUncle_When_Imported_Then_TheBlockIsRefusedForUnclesHash()
        {
            var harness = await OmmerRuleHarness.CreateAsync();
            var uncles = new List<BlockHeader> { harness.UncleOfAncestorAt(2) };

            var result = await harness.ImportAsync(harness.Child(), uncles);

            Assert.Equal(new[] { BlockValidityCheck.UnclesHash }, result.FailedValidityChecks);
        }
    }
}
