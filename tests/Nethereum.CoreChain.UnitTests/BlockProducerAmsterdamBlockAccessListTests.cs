using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockProducerAmsterdamBlockAccessListTests
    {
        private static readonly byte[] EmptyBlockAccessListHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public async Task Given_AmsterdamFork_When_BlockProduced_Then_BlockAccessListHashIsStamped()
        {
            var (header, _) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();

            Assert.NotNull(header.SlotNumber);
            Assert.NotNull(header.BlockAccessListHash);
            Assert.Equal(32, header.BlockAccessListHash.Length);
            Assert.NotEqual(EmptyBlockAccessListHash, header.BlockAccessListHash);
            Assert.IsType<AmsterdamBlockHeaderCodec>(BlockHeaderCodecSelector.ForHeader(header));
        }

        [Fact]
        public async Task Given_BlockWithWrongBalHash_When_Imported_Then_Rejected()
        {
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();
            var tampered = AmsterdamBlockPipelineHarness.CloneHeader(header);
            tampered.BlockAccessListHash = new byte[32];
            var importer = await AmsterdamBlockPipelineHarness.CreateImporterAsync();

            var result = await importer.ImportAsync(tampered, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.False(result.StateRootMismatch);
            Assert.True(result.BlockAccessListHashMismatch);
            Assert.False(result.RootMatches);
            Assert.Contains("blockAccessListHash", result.FailedChecks);
            Assert.Null(result.BlockHash);
        }

        [Fact]
        public async Task Given_ValidAmsterdamBlock_When_Imported_Then_Accepted()
        {
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();
            var importer = await AmsterdamBlockPipelineHarness.CreateImporterAsync();

            var result = await importer.ImportAsync(header, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.True(result.RootMatches, string.Join(",", result.FailedChecks));
            Assert.False(result.BlockAccessListHashMismatch);
            Assert.NotNull(result.BlockHash);
        }

        // AMS-7928-05 (EIP-7928): "bal_items <= block_gas_limit // ITEM_COST". The block's
        private static async Task<BlockImporterResult> ImportWithGasLimitAsync(
            BlockHeader header, List<ISignedTransaction> txs, long gasLimit)
        {
            var retargeted = AmsterdamBlockPipelineHarness.CloneHeader(header);
            retargeted.GasLimit = gasLimit;
            var importer = await AmsterdamBlockPipelineHarness.CreateImporterAsync();
            return await importer.ImportAsync(retargeted, txs, uncles: null, withdrawals: null, CancellationToken.None);
        }

        private static async Task<long> BlockAccessListItemsAsync(
            BlockHeader header, List<ISignedTransaction> txs)
        {
            var importer = await AmsterdamBlockPipelineHarness.CreateImporterAsync();
            var accepted = await importer.ImportAsync(header, txs, uncles: null, withdrawals: null, CancellationToken.None);
            Assert.True(accepted.RootMatches, string.Join(",", accepted.FailedChecks));
            return BlockAccessListSizeRule.CountItems(accepted.BlockAccessList);
        }

        [Fact]
        public async Task Given_ABlockWhoseAccessListExceedsTheBlockGasLimit_When_Imported_Then_Rejected()
        {
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync(new List<ISignedTransaction>());
            var items = await BlockAccessListItemsAsync(header, txs);
            var gasLimit = (items - 1) * GasConstants.EIP7928_ITEM_COST;
            Assert.True(gasLimit >= header.GasUsed,
                "the gas limit must still cover the block's gas used, or gasCapacity refuses it first");

            var result = await ImportWithGasLimitAsync(header, txs, gasLimit);

            Assert.True(result.BlockAccessListGasLimitExceeded);
            Assert.False(result.GasCapacityExceeded);
            Assert.False(result.BlockAccessListHashMismatch);
            Assert.False(result.StateRootMismatch);
            Assert.False(result.RootMatches);
            Assert.Contains("blockAccessListGasLimit", result.FailedChecks);
            Assert.Null(result.BlockHash);
        }

        [Fact]
        public async Task Given_ABlockWhoseAccessListWouldExceedTheBlockGasLimit_When_Produced_Then_ItIsNotSealed()
        {
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync(new List<ISignedTransaction>());
            var items = await BlockAccessListItemsAsync(header, txs);

            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AmsterdamBlockPipelineHarness.ProduceBlockAsync(txs, (items - 1) * GasConstants.EIP7928_ITEM_COST));

            Assert.Contains("Block access list exceeds the block gas limit", refusal.Message);
        }

        [Fact]
        public async Task Given_ABlockWhoseAccessListFitsTheBlockGasLimit_When_Produced_Then_ItIsSealed()
        {
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync(new List<ISignedTransaction>());
            var items = await BlockAccessListItemsAsync(header, txs);

            var produced = await AmsterdamBlockPipelineHarness.ProduceBlockAsync(txs, items * GasConstants.EIP7928_ITEM_COST);

            Assert.NotNull(produced.Header.BlockAccessListHash);
            Assert.Equal(items * GasConstants.EIP7928_ITEM_COST, produced.Header.GasLimit);
        }

        [Fact]
        public async Task Given_ABlockWhoseAccessListFitsTheBlockGasLimit_When_Imported_Then_Accepted()
        {
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync(new List<ISignedTransaction>());
            var items = await BlockAccessListItemsAsync(header, txs);

            var result = await ImportWithGasLimitAsync(
                header, txs, items * GasConstants.EIP7928_ITEM_COST);

            Assert.False(result.BlockAccessListGasLimitExceeded);
            Assert.True(result.RootMatches, string.Join(",", result.FailedChecks));
            Assert.NotNull(result.BlockHash);
        }
    }
}
