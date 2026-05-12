using System.Collections.Generic;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests
{
    public class BinaryTrieWitnessExecutionTests
    {
        private readonly ITestOutputHelper _output;

        private const string SENDER_KEY = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string SENDER = TestTransactionHelper.GetDefaultSenderAddress();
        private const string CONTRACT = "0x1000000000000000000000000000000000000000";

        public BinaryTrieWitnessExecutionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ExecuteWithBinaryBlake3_ProducesNonZeroStateRoot()
        {
            var block = CreateSstoreBlock(BlockFeatureConfig.BinaryBlake3());
            var result = BlockExecutionHelper.ExecuteBlock(block);

            Assert.NotNull(result.StateRoot);
            Assert.NotEqual(new byte[32], result.StateRoot);
            Assert.True(result.TxResults.Count > 0);
            Assert.True(result.TxResults[0].Success);
            _output.WriteLine($"BLAKE3 state root: {result.StateRoot.ToHex(true)}");
            _output.WriteLine($"Gas used: {result.CumulativeGasUsed}");
        }

        [Fact]
        public void ExecuteWithBinaryPoseidon_ProducesNonZeroStateRoot()
        {
            var block = CreateSstoreBlock(BlockFeatureConfig.BinaryPoseidon());
            var result = BlockExecutionHelper.ExecuteBlock(block);

            Assert.NotNull(result.StateRoot);
            Assert.NotEqual(new byte[32], result.StateRoot);
            Assert.True(result.TxResults[0].Success);
            _output.WriteLine($"Poseidon state root: {result.StateRoot.ToHex(true)}");
        }

        [Fact]
        public void ExecuteWithPatricia_ProducesDifferentRootThanBinary()
        {
            var patriciaBlock = CreateSstoreBlock(BlockFeatureConfig.Prague);
            var binaryBlock = CreateSstoreBlock(BlockFeatureConfig.BinaryBlake3());

            var patriciaResult = BlockExecutionHelper.ExecuteBlock(patriciaBlock);
            var binaryResult = BlockExecutionHelper.ExecuteBlock(binaryBlock);

            Assert.NotNull(patriciaResult.StateRoot);
            Assert.NotNull(binaryResult.StateRoot);
            Assert.NotEqual(patriciaResult.StateRoot, binaryResult.StateRoot);

            Assert.Equal(patriciaResult.CumulativeGasUsed, binaryResult.CumulativeGasUsed);

            _output.WriteLine($"Patricia root: {patriciaResult.StateRoot.ToHex(true)}");
            _output.WriteLine($"Binary root:   {binaryResult.StateRoot.ToHex(true)}");
            _output.WriteLine($"Gas (both):    {patriciaResult.CumulativeGasUsed}");
        }

        [Fact]
        public void BinaryBlake3_DeterministicAcrossRuns()
        {
            var block1 = CreateSstoreBlock(BlockFeatureConfig.BinaryBlake3());
            var block2 = CreateSstoreBlock(BlockFeatureConfig.BinaryBlake3());

            var result1 = BlockExecutionHelper.ExecuteBlock(block1);
            var result2 = BlockExecutionHelper.ExecuteBlock(block2);

            Assert.Equal(result1.StateRoot, result2.StateRoot);
        }

        [Fact]
        public void BinaryPoseidon_AllOutputsMatchZisk()
        {
            var block = CreateSstoreBlock(BlockFeatureConfig.BinaryPoseidon());
            block.ProduceBlockCommitments = true;
            block.ComputePostStateRoot = true;
            var result = BlockExecutionHelper.ExecuteBlock(block);

            _output.WriteLine($"state_root:  {result.StateRoot.ToHex(true)}");
            _output.WriteLine($"block_hash:  {result.BlockHash.ToHex(true)}");
            _output.WriteLine($"gas:         {result.CumulativeGasUsed}");

            Assert.Equal("0x0b3895e5a62c18c1dfe47b8ed0eba0522bea1e7fc444801ab6858a0520d758ec", result.StateRoot.ToHex(true));
            Assert.Equal("0x59874593eba5c3bbe3bdd5d2d82f020349597a6b7119a8edc137411736f4fb94", result.BlockHash.ToHex(true));
            Assert.Equal(43106L, result.CumulativeGasUsed);
        }

        [Fact]
        public void Patricia_AllOutputsMatchZisk()
        {
            var block = CreateSstoreBlock(BlockFeatureConfig.Prague);
            block.ProduceBlockCommitments = true;
            block.ComputePostStateRoot = true;
            var result = BlockExecutionHelper.ExecuteBlock(block);

            _output.WriteLine($"state_root:  {result.StateRoot.ToHex(true)}");
            _output.WriteLine($"block_hash:  {result.BlockHash.ToHex(true)}");
            _output.WriteLine($"tx_root:     {result.TransactionsRoot.ToHex(true)}");
            _output.WriteLine($"receipt_root: {result.ReceiptsRoot.ToHex(true)}");
            _output.WriteLine($"gas:         {result.CumulativeGasUsed}");

            Assert.Equal("0x2fc84afa1e66eaef44a179f33c5a2bbacfa458bdc97b12cd7c3c29750dd8142d", result.StateRoot.ToHex(true));
            Assert.Equal("0xade9ca96b102dc95cc86b1fd6e21b796f6fd334b909aaac3268b73578e8e543f", result.BlockHash.ToHex(true));
            Assert.Equal(43106L, result.CumulativeGasUsed);
        }

        [Fact]
        public void BinaryPoseidon_DeterministicAcrossRuns()
        {
            var block1 = CreateSstoreBlock(BlockFeatureConfig.BinaryPoseidon());
            var block2 = CreateSstoreBlock(BlockFeatureConfig.BinaryPoseidon());

            var result1 = BlockExecutionHelper.ExecuteBlock(block1);
            var result2 = BlockExecutionHelper.ExecuteBlock(block2);

            Assert.Equal(result1.StateRoot, result2.StateRoot);
        }

        private static BlockWitnessData CreateSstoreBlock(BlockFeatureConfig features)
        {
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };

            return new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = false, ComputePostStateRoot = true,
                Features = features,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(
                        CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 100000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount
                    {
                        Address = SENDER, Balance = new EvmUInt256(1000000000000000000),
                        Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = CONTRACT, Balance = EvmUInt256.Zero,
                        Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>()
                    }
                }
            };
        }
    }
}
