using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockHeaderAdmissionTests
    {
        private static readonly BigInteger ChainId = 1337;
        private const long BlockGasLimit = 30_000_000;

        private static async Task<BlockExecutionResult> ExecuteHeaderOnlyAsync(
            HardforkName fork, long gasUsed, long? blobGasUsed)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, fork);
            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = BlockGasLimit,
                BaseFee = 0,
                Hardfork = fork.ToString()
            };
            var trieNodeStore = new InMemoryContentNodeStore();

            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(fork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                GasLimit = BlockGasLimit,
                GasUsed = gasUsed,
                BlobGasUsed = blobGasUsed ?? 0,
                ExcessBlobGas = 0,
                BaseFee = 0,
                Coinbase = "0x90F79bf6EB2c4f870365E785982E1f101E93b906",
                ParentHash = new byte[32]
            };

            var result = await engine.ExecuteAsync(
                header, new TxEntry[0], uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.Null(result.Exception);
            return result;
        }

        private static long MaxBlobGasAt(HardforkName fork)
        {
            var config = new ChainConfig { ChainId = ChainId, Hardfork = fork.ToString() };
            return (long)config.GetHardforkConfig().MaxBlobsPerBlock * BlobGasCalculator.GAS_PER_BLOB;
        }

        [Fact]
        public async Task Given_AHeaderDeclaringMoreGasUsedThanItsOwnGasLimit_When_Executed_Then_TheBlockIsRefusedForGasCapacity()
        {
            var result = await ExecuteHeaderOnlyAsync(HardforkName.Prague, BlockGasLimit + 1, blobGasUsed: null);

            Assert.True(result.GasCapacityExceeded);
            Assert.False(result.BlobGasCapacityExceeded);
            Assert.Null(result.PreStateRoot);
            Assert.Null(result.PostStateRoot);
            Assert.Empty(result.Receipts);
            Assert.Equal(0, result.GasUsed);
        }

        [Fact]
        public async Task Given_AHeaderDeclaringGasUsedExactlyAtItsGasLimit_When_Executed_Then_ItIsNotRefusedForGasCapacity()
        {
            var result = await ExecuteHeaderOnlyAsync(HardforkName.Prague, BlockGasLimit, blobGasUsed: null);

            Assert.False(result.GasCapacityExceeded);
        }

        [Fact]
        public async Task Given_AHeaderDeclaringMoreBlobGasThanTheForkAllows_When_Executed_Then_TheBlockIsRefusedForBlobGasCapacity()
        {
            var maxBlobGas = MaxBlobGasAt(HardforkName.Prague);
            Assert.True(maxBlobGas > 0, "the fork must allow some blob gas, or this proves nothing");

            var result = await ExecuteHeaderOnlyAsync(HardforkName.Prague, gasUsed: 0, blobGasUsed: maxBlobGas + 1);

            Assert.True(result.BlobGasCapacityExceeded);
            Assert.False(result.GasCapacityExceeded);
            Assert.Null(result.PreStateRoot);
            Assert.Null(result.PostStateRoot);
            Assert.Empty(result.Receipts);
            Assert.Equal(0, result.GasUsed);
        }

        [Fact]
        public async Task Given_AHeaderDeclaringExactlyMaxBlobsWorthOfBlobGas_When_Executed_Then_ItIsNotRefusedForBlobGasCapacity()
        {
            var result = await ExecuteHeaderOnlyAsync(
                HardforkName.Prague, gasUsed: 0, blobGasUsed: MaxBlobGasAt(HardforkName.Prague));

            Assert.False(result.BlobGasCapacityExceeded);
        }

        [Fact]
        public async Task Given_AHeaderFailingBothCapacityChecks_When_Executed_Then_TheGasCapacityRefusalIsTheOneReported()
        {
            var result = await ExecuteHeaderOnlyAsync(
                HardforkName.Prague, BlockGasLimit + 1, blobGasUsed: MaxBlobGasAt(HardforkName.Prague) + 1);

            Assert.True(result.GasCapacityExceeded);
            Assert.False(result.BlobGasCapacityExceeded);
        }
    }
}
