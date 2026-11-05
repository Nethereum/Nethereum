using System.Text;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class SealRuleTests
    {
        private sealed class DaoForkAt : IChainActivations
        {
            private readonly long _daoBlock;

            public DaoForkAt(long daoBlock) => _daoBlock = daoBlock;

            public HardforkName ResolveAt(long blockNumber, ulong timestamp) =>
                blockNumber >= _daoBlock ? HardforkName.DaoFork : HardforkName.Homestead;
        }

        private static readonly byte[] DaoExtra = Encoding.ASCII.GetBytes("dao-hard-fork");

        private sealed class DaoForkThenTangerine : IChainActivations
        {
            private readonly long _daoBlock;
            private readonly long _tangerineBlock;

            public DaoForkThenTangerine(long daoBlock, long tangerineBlock)
            {
                _daoBlock = daoBlock;
                _tangerineBlock = tangerineBlock;
            }

            public HardforkName ResolveAt(long blockNumber, ulong timestamp) =>
                blockNumber >= _tangerineBlock ? HardforkName.TangerineWhistle
                : blockNumber >= _daoBlock ? HardforkName.DaoFork
                : HardforkName.Homestead;
        }

        [Fact]
        public async Task Given_DaoForkAt994AndTangerineWhistleAt999_When_Block1001LacksTheDaoExtraData_Then_TheBlockIsRefusedForDaoExtraData()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.TangerineWhistle, activations: new DaoForkThenTangerine(994, 999));

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.Equal(new[] { BlockValidityCheck.DaoExtraData }, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_DaoForkAt990AndTangerineWhistleAt995_When_Block1001IsPastTheTenBlockRange_Then_NoDaoExtraDataRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.TangerineWhistle, activations: new DaoForkThenTangerine(990, 995));

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.DoesNotContain(BlockValidityCheck.DaoExtraData, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_DaoForkAt995_When_Block1001LacksTheDaoExtraData_Then_TheBlockIsRefusedForDaoExtraData()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.DaoFork, activations: new DaoForkAt(995));

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.Equal(new[] { BlockValidityCheck.DaoExtraData }, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_DaoForkAt995_When_Block1001CarriesTheDaoExtraData_Then_NoDaoExtraDataRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.DaoFork, activations: new DaoForkAt(995));
            var header = harness.ValidChild();
            header.ExtraData = DaoExtra;

            var result = await harness.ImportAsync(header);

            Assert.DoesNotContain(BlockValidityCheck.DaoExtraData, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_DaoForkAt980_When_Block1001IsPastTheTenBlockRange_Then_NoDaoExtraDataRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.DaoFork, activations: new DaoForkAt(980));

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.DoesNotContain(BlockValidityCheck.DaoExtraData, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_APreMergeChainWithAClock_When_TheHeaderIs16sInTheFuture_Then_TheBlockIsRefusedAsFuture()
        {
            var timestamp = HeaderRuleHarness.ParentTimestamp + 12;
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.Berlin, options: new BlockValidationOptions { UnixClock = () => timestamp - 16 });

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.Equal(new[] { BlockValidityCheck.FutureBlock }, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_APreMergeChainWithAClock_When_TheHeaderIs15sInTheFuture_Then_NoFutureBlockRefusal()
        {
            var timestamp = HeaderRuleHarness.ParentTimestamp + 12;
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.Berlin, options: new BlockValidationOptions { UnixClock = () => timestamp - 15 });

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.DoesNotContain(BlockValidityCheck.FutureBlock, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_APreMergeChainWithoutAClock_When_TheHeaderIsFarInTheFuture_Then_NoFutureBlockRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Berlin);

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.DoesNotContain(BlockValidityCheck.FutureBlock, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_APostMergeChainWithAClock_When_TheHeaderIsFarInTheFuture_Then_NoFutureBlockRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.Prague, options: new BlockValidationOptions { UnixClock = () => 0 });

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.DoesNotContain(BlockValidityCheck.FutureBlock, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_AnUncle16sInTheFuture_When_Imported_Then_TheUncleIsNotRefusedAsFuture()
        {
            var clock = OmmerRuleHarness.TipTimestamp - 3;
            var harness = await OmmerRuleHarness.CreateAsync(() => clock);
            var uncle = harness.UncleOfAncestorAt(2);
            uncle.Timestamp = clock + 16;
            uncle.Difficulty = (Nethereum.Util.EvmUInt256)EthashDifficulty.Calculate(
                OmmerRuleHarness.Fork, (ulong)uncle.Timestamp, harness.AncestorAt(2));
            var uncles = new System.Collections.Generic.List<BlockHeader> { uncle };

            var result = await harness.ImportAsync(harness.Child(uncles), uncles);

            Assert.DoesNotContain(BlockValidityCheck.Uncles, result.FailedValidityChecks);
            Assert.DoesNotContain(BlockValidityCheck.FutureBlock, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_ACliqueChain_When_TheExtraDataCarriesTheSealAndSigners_Then_ExtraDataSizeIsNotJudged()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.Prague, options: new BlockValidationOptions { SealedByClique = true });
            var header = harness.ValidChild();
            header.ExtraData = new byte[97];
            header.Difficulty = 2;
            header.Nonce = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };

            var result = await harness.ImportAsync(header);

            Assert.DoesNotContain(BlockValidityCheck.ExtraDataSize, result.FailedValidityChecks);
            Assert.DoesNotContain(BlockValidityCheck.Difficulty, result.FailedValidityChecks);
            Assert.DoesNotContain(BlockValidityCheck.Nonce, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_ACliqueChain_When_ACommonRuleFails_Then_TheGenericVerdictNamesIt()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.Prague, options: new BlockValidationOptions { SealedByClique = true });
            var header = harness.ValidChild();
            header.Difficulty = 2;
            header.GasUsed = header.GasLimit + 1;

            var result = await harness.ImportAsync(header);

            Assert.Equal(new[] { BlockValidityCheck.GasCapacity }, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_ACliqueChain_When_TheHeaderSharesTheParentTimestamp_Then_NoTimestampRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.Prague, options: new BlockValidationOptions { SealedByClique = true });
            var header = harness.ValidChild();
            header.Difficulty = 2;
            header.Timestamp = HeaderRuleHarness.ParentTimestamp;

            var result = await harness.ImportAsync(header);

            Assert.DoesNotContain(BlockValidityCheck.Timestamp, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_AProofOfStakeChain_When_TheHeaderSharesTheParentTimestamp_Then_TheBlockIsRefusedForTimestamp()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.Timestamp = HeaderRuleHarness.ParentTimestamp;

            var result = await harness.ImportAsync(header);

            Assert.Contains(BlockValidityCheck.Timestamp, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_ACliqueChain_When_TheBodyCarriesAnUncle_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await HeaderRuleHarness.CreateAsync(
                HardforkName.Prague, options: new BlockValidationOptions { SealedByClique = true });

            var result = await harness.ImportAsync(
                harness.ValidChild(), new System.Collections.Generic.List<BlockHeader> { harness.ValidChild() });

            Assert.Equal(new[] { BlockValidityCheck.Uncles }, result.FailedValidityChecks);
        }
    }
}
