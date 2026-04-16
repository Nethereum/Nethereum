using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Core.Tests;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BlockSyncTests
    {
        private readonly ITestOutputHelper _output;
        private const string SENDER_KEY = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string SENDER = TestTransactionHelper.GetDefaultSenderAddress();
        private const string RECEIVER = "0x1000000000000000000000000000000000000001";
        private const string COINBASE = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba";

        public BlockSyncTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void BlockWitness_RoundtripSerializeDeserialize()
        {
            var block = CreateSignedTwoTxBlock();
            var bytes = BinaryBlockWitness.Serialize(block);
            var deserialized = BinaryBlockWitness.Deserialize(bytes);

            Assert.Equal(block.BlockNumber, deserialized.BlockNumber);
            Assert.Equal(block.Transactions.Count, deserialized.Transactions.Count);
            Assert.Equal(block.Accounts.Count, deserialized.Accounts.Count);
            Assert.Equal(block.Transactions[0].From, deserialized.Transactions[0].From);
            Assert.NotNull(deserialized.Transactions[0].RlpEncoded);
            Assert.True(deserialized.Transactions[0].RlpEncoded.Length > 0);
        }

        [Fact]
        public void BlockSync_ExecuteTwoTransfers()
        {
            var block = CreateSignedTwoTxBlock();
            var result = BlockExecutionHelper.ExecuteBlock(block);

            Assert.Equal(2, result.TxResults.Count);
            Assert.True(result.TxResults[0].Success, $"Tx0: {result.TxResults[0].Error}");
            Assert.True(result.TxResults[1].Success, $"Tx1: {result.TxResults[1].Error}");
            Assert.True(result.CumulativeGasUsed > 0);
            Assert.NotNull(result.StateRoot);

            _output.WriteLine($"Gas: {result.CumulativeGasUsed}, State root: 0x{result.StateRoot.ToHex()}");
        }

        [Fact]
        public void BlockWitnessSync_SerializeExecute()
        {
            var block = CreateSignedTwoTxBlock();
            var bytes = BinaryBlockWitness.Serialize(block);
            var deserialized = BinaryBlockWitness.Deserialize(bytes);
            var result = BlockExecutionHelper.ExecuteBlock(deserialized);

            Assert.True(result.TxResults[0].Success);
            Assert.True(result.TxResults[1].Success);
        }

        [Fact]
        public void BlockSync_ProducesReceiptsAndBloom()
        {
            var block = CreateSignedTwoTxBlock();
            var result = BlockExecutionHelper.ExecuteBlock(block);

            Assert.Equal(2, result.Receipts.Count);
            Assert.True(result.Receipts[0].HasSucceeded.Value);
            Assert.True(result.Receipts[1].HasSucceeded.Value);
            Assert.True((long)result.Receipts[1].CumulativeGasUsed > (long)result.Receipts[0].CumulativeGasUsed);
        }

        [Fact]
        public void BlockSync_StateCarriesForwardBetweenTransactions()
        {
            var block = CreateSignedValueTransferBlock();
            var result = BlockExecutionHelper.ExecuteBlock(block);

            Assert.True(result.TxResults[0].Success, $"Tx0: {result.TxResults[0].Error}");
            Assert.True(result.TxResults[1].Success, $"Tx1: {result.TxResults[1].Error}");

            var receiverState = result.FinalExecutionState.CreateOrGetAccountExecutionState(RECEIVER);
            Assert.Equal(new EvmUInt256(1500), receiverState.Balance.GetTotalBalance());
        }

        private static BlockWitnessData CreateSignedTwoTxBlock()
        {
            return new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1, Coinbase = COINBASE,
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                Features = BlockFeatureConfig.Prague,
                ComputePostStateRoot = true,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(RECEIVER, EvmUInt256.Zero, 0, 10, 21000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedTransfer(RECEIVER, EvmUInt256.Zero, 1, 10, 21000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(10000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = RECEIVER, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
                }
            };
        }

        private static BlockWitnessData CreateSignedValueTransferBlock()
        {
            return new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1, Coinbase = COINBASE,
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(RECEIVER, new EvmUInt256(1000), 0, 10, 21000, SENDER_KEY),
                    TestTransactionHelper.CreateSignedTransfer(RECEIVER, new EvmUInt256(500), 1, 10, 21000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(10000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = RECEIVER, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
                }
            };
        }
    }
}
