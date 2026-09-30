using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.EVM.Witness;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class QuickStartDocExampleTests
    {
        private const string SenderKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private const string ContractAddress = "0x1000000000000000000000000000000000000000";

        [NethereumDocExample(DocSection.EvmSimulator, "block-execution", "Quick start: execute a block from a witness and read its gas and post-state root", Order = 1)]
        [Fact]
        public void ExecuteABlockFromAWitness()
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();
            var storesFortyTwoInSlotZero = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };

            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1_000_000,
                BaseFee = 7,
                BlockGasLimit = 30_000_000,
                ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(
                        ContractAddress, new byte[0], EvmUInt256.Zero, 0, 10, 100_000, SenderKey)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = sender, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = ContractAddress, Balance = EvmUInt256.Zero, Nonce = 0, Code = storesFortyTwoInSlotZero, Storage = new List<WitnessStorageSlot>() }
                }
            };

            var result = BlockExecutionHelper.ExecuteBlock(block);

            Assert.True(result.TxResults[0].Success, result.TxResults[0].Error);
            Assert.True(result.CumulativeGasUsed > 21_000);
            Assert.NotNull(result.StateRoot);
        }
    }
}
