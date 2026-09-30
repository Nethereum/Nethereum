using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockValidityCheckParityTests
    {
        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "A failing block validity check is named on the importer result", Order = 2)]
        public async Task Given_ABlockWhoseHeaderGasUsedExceedsItsGasLimit_When_Imported_Then_TheImporterResultNamesGasCapacity()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var txs = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(nonce: 0) };
            var produced = await author.ProduceAsync(txs);

            var tampered = BlockPipelineHarness.CloneHeader(produced.Header);
            tampered.GasUsed = tampered.GasLimit + 1;

            var follower = await BlockPipelineHarness.CreateAsync();
            var result = await follower.ImportAsync(tampered, txs);

            Assert.False(result.RootMatches);
            Assert.Null(result.BlockHash);
            Assert.Contains(
                "gasCapacity",
                result.FailedChecks);
        }

        [Fact]
        public void Given_EveryBlockExecutionFailedCheck_Then_TheImporterResultExposesTheSameSet()
        {
            var execution = new BlockExecutionResult
            {
                Fork = Nethereum.EVM.HardforkName.Prague,
                PreStateRoot = null,
                PostStateRoot = null,
                Receipts = new List<TransactionExecutionResult>(),
                Logs = new List<Log>(),
                StateRootMismatch = true,
                ReceiptsRootMismatch = true,
                LogsBloomMismatch = true,
                GasUsedMismatch = true,
                GasCapacityExceeded = true,
                BlobGasCapacityExceeded = true,
                BlobGasUsedMismatch = true,
                ExcessBlobGasMismatch = true,
                BlobFieldFormatMismatch = true,
                BaseFeeMismatch = true,
                BlockAccessListHashMismatch = true,
                BlockAccessListGasLimitExceeded = true,
                BlockAccessListMalformed = true,
                RequestsHashMismatch = true,
                ContainsInvalidTransaction = true,
                GasLimitBoundViolated = true,
                WithdrawalsRootMismatch = true
            };

            var importer = new BlockImporterResult();
            foreach (var flag in typeof(BlockImporterResult)
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            {
                flag.SetValue(importer, true);
            }

            Assert.Equal(
                Enum.GetValues<BlockValidityCheck>().OrderBy(c => c).ToList(),
                execution.FailedValidityChecks.OrderBy(c => c).ToList());

            Assert.Equal(
                execution.FailedChecks.OrderBy(c => c).ToList(),
                importer.FailedChecks.OrderBy(c => c).ToList());
        }
    }
}
