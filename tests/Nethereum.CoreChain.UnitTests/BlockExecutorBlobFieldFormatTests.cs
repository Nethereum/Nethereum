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

        private static readonly BigInteger ChainId = 1337;

        private static async Task<(BlockExecutor Engine, byte[] GenesisHash)> BuildEngineAsync(HardforkName resolvedFork)
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
            var blockStore = new InMemoryBlockStore();
            var genesisHash = await ShapedHeaders.SaveGenesisAsync(blockStore, resolvedFork, BlockGasLimit);

            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(resolvedFork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: config.ConfigForFork,
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);
            return (engine, genesisHash);
        }

        private static BlockHeader HeaderWith(HardforkName fork, byte[] genesisHash, long? blobGasUsed, long? excessBlobGas)
        {
            var header = ShapedHeaders.Create(fork, 1, 1_700_000_000, BlockGasLimit, 0, 0);
            header.ParentHash = genesisHash;
            header.BlobGasUsed = blobGasUsed;
            header.ExcessBlobGas = excessBlobGas;
            if (fork < HardforkName.Paris)
                header.Difficulty = (Nethereum.Util.EvmUInt256)EthashDifficulty.Calculate(
                    fork, (ulong)header.Timestamp, 0, (ulong)ShapedHeaders.GenesisTimestamp, 0, false);
            return header;
        }

        [Fact]
        public async Task Given_APreCancunHeaderThatDeclaresBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsFlagged()
        {
            var (engine, genesisHash) = await BuildEngineAsync(HardforkName.London);
            var header = HeaderWith(HardforkName.London, genesisHash, blobGasUsed: 0, excessBlobGas: 0);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.True(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APostCancunHeaderMissingBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsFlagged()
        {
            var (engine, genesisHash) = await BuildEngineAsync(HardforkName.Prague);
            var header = HeaderWith(HardforkName.Prague, genesisHash, blobGasUsed: null, excessBlobGas: null);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.True(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APostCancunHeaderWithOnlyOneOfTheTwoBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsFlagged()
        {
            var (engine, genesisHash) = await BuildEngineAsync(HardforkName.Prague);
            var header = HeaderWith(HardforkName.Prague, genesisHash, blobGasUsed: 0, excessBlobGas: null);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.True(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APostCancunHeaderWithBlobFields_When_ValidatingImport_Then_BlobFieldFormatMismatchIsNotFlagged()
        {
            var (engine, genesisHash) = await BuildEngineAsync(HardforkName.Prague);
            var header = HeaderWith(HardforkName.Prague, genesisHash, blobGasUsed: 0, excessBlobGas: 0);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions { Role = BlockExecutionRole.Validating });

            Assert.False(result.BlobFieldFormatMismatch);
        }

        [Fact]
        public async Task Given_APreCancunHeaderThatDeclaresBlobFields_When_Building_Then_TheFormatGuardIsNotEnforced()
        {
            var (engine, genesisHash) = await BuildEngineAsync(HardforkName.London);
            var header = HeaderWith(HardforkName.London, genesisHash, blobGasUsed: 0, excessBlobGas: 0);

            var result = await engine.ExecuteAsync(
                header, Array.Empty<TxEntry>(), uncles: null, withdrawals: null,
                options: new BlockExecutionOptions());

            Assert.False(result.BlobFieldFormatMismatch);
        }
    }
}
