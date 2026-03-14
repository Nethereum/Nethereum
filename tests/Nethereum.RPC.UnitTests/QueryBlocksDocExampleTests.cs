using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.RPC.UnitTests
{
    public class QueryBlocksDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-blocks", "BlockParameter.CreatePending returns pending tag")]
        public void BlockParameter_CreatePending_ShouldReturnPendingTag()
        {
            var pending = BlockParameter.CreatePending();
            Assert.Equal(BlockParameter.BlockParameterType.pending, pending.ParameterType);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-blocks", "BlockParameter.CreateLatest returns latest tag")]
        public void BlockParameter_CreateLatest_ShouldReturnLatestTag()
        {
            var latest = BlockParameter.CreateLatest();
            Assert.Equal(BlockParameter.BlockParameterType.latest, latest.ParameterType);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-blocks", "BlockParameter from specific block number")]
        public void BlockParameter_FromBlockNumber_ShouldStoreNumber()
        {
            var blockParam = new BlockParameter(8257129);
            Assert.Equal(BlockParameter.BlockParameterType.blockNumber, blockParam.ParameterType);
            Assert.Equal(new HexBigInteger(8257129).Value, blockParam.BlockNumber.Value);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-blocks", "TransactionReceipt.HasErrors detects reverts")]
        public void TransactionReceipt_HasErrors_ShouldDetectRevert()
        {
            var successReceipt = new TransactionReceipt { Status = new HexBigInteger(1) };
            Assert.False(successReceipt.HasErrors());

            var revertReceipt = new TransactionReceipt { Status = new HexBigInteger(0) };
            Assert.True(revertReceipt.HasErrors());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "pending-transactions", "BlockParameter.CreatePending for mempool queries")]
        public void PendingBlock_ShouldUsePendingParameter()
        {
            var pending = BlockParameter.CreatePending();
            Assert.Equal(BlockParameter.BlockParameterType.pending, pending.ParameterType);

            var latest = BlockParameter.CreateLatest();
            Assert.NotEqual(pending.ParameterType, latest.ParameterType);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-blocks", "Contract detection via code length")]
        public void ContractDetection_EmptyCode_ShouldIndicateEOA()
        {
            var eoaCode = "0x";
            var isContract = eoaCode != null && eoaCode != "0x";
            Assert.False(isContract);

            var contractCode = "0x6080604052";
            var isContract2 = contractCode != null && contractCode != "0x";
            Assert.True(isContract2);
        }
    }
}
