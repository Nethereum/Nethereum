using System;
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
    public class BlockExecutorBlobFieldFormatTests
    {
        private const long BlockGasLimit = 30_000_000;
        private const string Coinbase = "0x0000000000000000000000000000000000009999";

        private static readonly BigInteger ChainId = 1337;

        private static async Task<BlockExecutor> BuildEngineAsync(HardforkName resolvedFork)
        {
            var stateStore = new InMemoryStateStore();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, resolvedFork);

            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = BlockGasLimit,
                BaseFee = 0
            };

            var trieNodeStore = new InMemoryContentNodeStore();

            return new BlockExecutor(
                stateStore,
                new InMemoryBlockStore(),
                new FixedChainActivations(resolvedFork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: config.ConfigForFork,
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);
        }

        private static BlockHeader HeaderWith(long? blobGasUsed, long? excessBlobGas) => new BlockHeader
        {
            BlockNumber = 1,
            Timestamp = 1_700_000_000,
            GasLimit = BlockGasLimit,
            GasUsed = 0,
            BaseFee = 0,
            Coinbase = Coinbase,
            ParentHash = new byte[32],
            Difficulty = 0,
            MixHash = new byte[32],
            BlobGasUsed = blobGasUsed,
            ExcessBlobGas = excessBlobGas
        };

        [Fact]
        public async Task Given_APreCancunHeaderThatDeclaresBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsFlagged()
        {
            var engine = await BuildEngineAsync(HardforkName.London);
            var header = HeaderWith(blobGasUsed: 0, excessBlobGas: 0);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.True(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APostCancunHeaderMissingBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsFlagged()
        {
            var engine = await BuildEngineAsync(HardforkName.Prague);
            var header = HeaderWith(blobGasUsed: null, excessBlobGas: null);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.True(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APostCancunHeaderWithOnlyOneOfTheTwoBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsFlagged()
        {
            var engine = await BuildEngineAsync(HardforkName.Prague);
            var header = HeaderWith(blobGasUsed: 0, excessBlobGas: null);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.True(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APostCancunHeaderWithBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsNotFlagged()
        {
            var engine = await BuildEngineAsync(HardforkName.Prague);
            var header = HeaderWith(blobGasUsed: 0, excessBlobGas: 0);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.False(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APreCancunHeaderThatDeclaresBlobFields_When_Building_Then_TheFormatGuardIsNotEnforced()
        {
            var engine = await BuildEngineAsync(HardforkName.London);
            var header = HeaderWith(blobGasUsed: 0, excessBlobGas: 0);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions());

            Assert.False(result.BlobFieldFormatMismatch);
        }
    }
}
