using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class HeaderRuleTests
    {
        private static void AssertRefusedOnlyFor(BlockValidityCheck check, BlockImporterResult result)
        {
            Assert.Equal(new[] { check }, result.FailedValidityChecks);
            Assert.Null(result.ComputedStateRoot);
            Assert.Null(result.BlockHash);
        }

        private static void AssertNotRefusedFor(BlockValidityCheck check, BlockImporterResult result) =>
            Assert.DoesNotContain(check, result.FailedValidityChecks);

        [Fact]
        public async Task Given_AParentAt1000_When_TheHeaderNumberIs1002_Then_TheBlockIsRefusedForBlockNumber()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.BlockNumber = 1002;

            AssertRefusedOnlyFor(BlockValidityCheck.BlockNumber, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AParentAt1000_When_TheHeaderNumberIs1001_Then_NoBlockNumberRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);

            AssertNotRefusedFor(BlockValidityCheck.BlockNumber, await harness.ImportAsync(harness.ValidChild()));
        }

        [Fact]
        public async Task Given_AParentAtTime1700000000_When_TheHeaderTimeIsTheSame_Then_TheBlockIsRefusedForTimestamp()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.Timestamp = HeaderRuleHarness.ParentTimestamp;

            AssertRefusedOnlyFor(BlockValidityCheck.Timestamp, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AParentAtTime1700000000_When_TheHeaderTimeIsOneSecondLater_Then_NoTimestampRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.Timestamp = HeaderRuleHarness.ParentTimestamp + 1;

            AssertNotRefusedFor(BlockValidityCheck.Timestamp, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_33BytesOfExtraData_When_ImportedOnPostMergeRules_Then_TheBlockIsRefusedForExtraDataSize()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.ExtraData = new byte[33];

            AssertRefusedOnlyFor(BlockValidityCheck.ExtraDataSize, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_32BytesOfExtraData_When_ImportedOnPostMergeRules_Then_NoExtraDataSizeRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.ExtraData = new byte[32];

            AssertNotRefusedFor(BlockValidityCheck.ExtraDataSize, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AGasLimitOf2Pow63_When_Imported_Then_TheBlockIsRefusedForGasLimitTooBig()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.GasLimit = long.MinValue;
            header.GasUsed = 0;

            AssertRefusedOnlyFor(BlockValidityCheck.GasLimitTooBig, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AGasUsedAboveTheGasLimit_When_Imported_Then_TheBlockIsRefusedForGasCapacity()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.GasUsed = header.GasLimit + 1;

            AssertRefusedOnlyFor(BlockValidityCheck.GasCapacity, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AParentGasLimitOf1024000_When_TheHeaderGasLimitIs1025000_Then_TheBlockIsRefusedForGasLimitBound()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague, parentGasLimit: 1_024_000);
            var header = harness.ValidChild();
            header.GasLimit = 1_025_000;

            AssertRefusedOnlyFor(BlockValidityCheck.GasLimitBound, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AParentGasLimitOf1024000_When_TheHeaderGasLimitIs1024999_Then_NoGasLimitBoundRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague, parentGasLimit: 1_024_000);
            var header = harness.ValidChild();
            header.GasLimit = 1_024_999;

            AssertNotRefusedFor(BlockValidityCheck.GasLimitBound, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AHeaderGasLimitBelow5000_When_Imported_Then_TheBlockIsRefusedForGasLimitBound()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague, parentGasLimit: 5_000);
            var header = harness.ValidChild();
            header.GasLimit = 4_999;

            AssertRefusedOnlyFor(BlockValidityCheck.GasLimitBound, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_TheFirstLondonBlock_When_TheGasLimitIsTwiceTheParents_Then_NoGasLimitBoundRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.London, parentGasLimit: 15_000_000, parentFork: HardforkName.Berlin);
            var header = harness.ValidChild();
            header.GasLimit = 30_000_000;

            AssertNotRefusedFor(BlockValidityCheck.GasLimitBound, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_TheFirstLondonBlock_When_TheGasLimitIsBeyondTheBoundOfTwiceTheParents_Then_TheBlockIsRefusedForGasLimitBound()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.London, parentGasLimit: 15_000_000, parentFork: HardforkName.Berlin);
            var header = harness.ValidChild();
            header.GasLimit = 30_000_000 + 30_000_000 / 1024;

            AssertRefusedOnlyFor(BlockValidityCheck.GasLimitBound, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_ABerlinBlock_When_ItDeclaresABaseFee_Then_TheBlockIsRefusedForBaseFee()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Berlin);
            var header = harness.ValidChild();
            header.BaseFee = 1_000;

            AssertRefusedOnlyFor(BlockValidityCheck.BaseFee, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_ALondonBlock_When_ItDeclaresAWrongBaseFee_Then_TheBlockIsRefusedForBaseFee()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.London);
            var header = harness.ValidChild();
            header.BaseFee = header.BaseFee + EvmUInt256.One;

            AssertRefusedOnlyFor(BlockValidityCheck.BaseFee, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_APragueBlock_When_ItLacksAParentBeaconRoot_Then_TheBlockIsRefusedForParentBeaconRootPresence()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.ParentBeaconBlockRoot = null;

            AssertRefusedOnlyFor(BlockValidityCheck.ParentBeaconRootPresence, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AShanghaiBlock_When_ItCarriesAParentBeaconRoot_Then_TheBlockIsRefusedForParentBeaconRootPresence()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Shanghai);
            var header = harness.ValidChild();
            header.ParentBeaconBlockRoot = new byte[32];

            AssertRefusedOnlyFor(BlockValidityCheck.ParentBeaconRootPresence, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AShanghaiBlock_When_ItLacksAWithdrawalsRoot_Then_TheBlockIsRefusedForWithdrawalsRootPresence()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Shanghai);
            var header = harness.ValidChild();
            header.WithdrawalsRoot = null;

            AssertRefusedOnlyFor(BlockValidityCheck.WithdrawalsRootPresence, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AParisBlock_When_ItCarriesAWithdrawalsRoot_Then_TheBlockIsRefusedForWithdrawalsRootPresence()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Paris);
            var header = harness.ValidChild();
            header.WithdrawalsRoot = Nethereum.Model.DefaultValues.EMPTY_TRIE_ROOT;

            AssertRefusedOnlyFor(BlockValidityCheck.WithdrawalsRootPresence, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_BlobGasUsedOf131073_When_Imported_Then_TheBlockIsRefusedForBlobGasCapacity()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.BlobGasUsed = 131_073;

            AssertRefusedOnlyFor(BlockValidityCheck.BlobGasCapacity, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_BlobGasUsedOf131072_When_Imported_Then_NoBlobGasCapacityRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.BlobGasUsed = 131_072;

            AssertNotRefusedFor(BlockValidityCheck.BlobGasCapacity, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AnAmsterdamBlock_When_SlotNumberIsMissing_Then_TheBlockIsRefusedForSlotNumberPresence()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Amsterdam);
            var header = harness.ValidChild();
            header.SlotNumber = null;

            AssertRefusedOnlyFor(BlockValidityCheck.SlotNumberPresence, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AnAmsterdamBlock_When_TheBlockAccessListHashIsMissing_Then_TheBlockIsRefusedForBlockAccessListHashPresence()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Amsterdam);
            var header = harness.ValidChild();
            header.BlockAccessListHash = null;

            AssertRefusedOnlyFor(BlockValidityCheck.BlockAccessListHashPresence, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_AnOsakaBlock_When_SlotNumberIsPresent_Then_TheBlockIsRefusedForSlotNumberPresence()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Osaka);
            var header = harness.ValidChild();
            header.SlotNumber = 1001;

            AssertRefusedOnlyFor(BlockValidityCheck.SlotNumberPresence, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_APostParisHeader_When_DifficultyIs1_Then_TheBlockIsRefusedForDifficulty()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.Difficulty = 1;

            AssertRefusedOnlyFor(BlockValidityCheck.Difficulty, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_APostParisHeader_When_NonceIsNonZero_Then_TheBlockIsRefusedForNonce()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.Nonce = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };

            AssertRefusedOnlyFor(BlockValidityCheck.Nonce, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_APostParisHeader_When_UnclesHashIsNotTheEmptyListHash_Then_TheBlockIsRefusedForUnclesHash()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.UnclesHash = new byte[32];

            AssertRefusedOnlyFor(BlockValidityCheck.UnclesHash, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_APreMergeParent_When_TheHeaderDifficultyIsNotTheCalculatedOne_Then_TheBlockIsRefusedForDifficulty()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Berlin);
            var header = harness.ValidChild();
            header.Difficulty = header.Difficulty + EvmUInt256.One;

            AssertRefusedOnlyFor(BlockValidityCheck.Difficulty, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_APreMergeParent_When_TheHeaderDifficultyIsTheCalculatedOne_Then_NoDifficultyRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Berlin);

            AssertNotRefusedFor(BlockValidityCheck.Difficulty, await harness.ImportAsync(harness.ValidChild()));
        }

        [Fact]
        public async Task Given_AnUnknownParentHash_When_Imported_Then_TheBlockIsRefusedForUnknownParentAndNothingIsPersisted()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.ParentHash = new byte[32];

            var result = await harness.ImportAsync(header);

            AssertRefusedOnlyFor(BlockValidityCheck.UnknownParent, result);
        }
    }
}
