using System.Collections.Generic;
using Nethereum.EVM.Witness;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests
{
    public class BinaryBlockWitnessStateTreeTests
    {
        private readonly ITestOutputHelper _output;
        private const string SENDER_KEY = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string SENDER = TestTransactionHelper.GetDefaultSenderAddress();
        private const string CONTRACT = "0x1000000000000000000000000000000000000000";

        public BinaryBlockWitnessStateTreeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RoundTrip_Patricia_DefaultFlags()
        {
            var block = CreateBlock(BlockFeatureConfig.Prague);
            var bytes = BinaryBlockWitness.Serialize(block);
            var decoded = BinaryBlockWitness.Deserialize(bytes);

            Assert.Equal(WitnessStateTreeType.Patricia, decoded.Features.StateTree);
            Assert.Equal(WitnessHashFunction.Keccak, decoded.Features.HashFunction);
            Assert.Equal(HardforkName.Prague, decoded.Features.Fork);
            _output.WriteLine($"Patricia witness: {bytes.Length} bytes, flags=0x{bytes[1]:X2}");
        }

        [Fact]
        public void RoundTrip_BinaryBlake3()
        {
            var block = CreateBlock(BlockFeatureConfig.BinaryBlake3());
            var bytes = BinaryBlockWitness.Serialize(block);
            var decoded = BinaryBlockWitness.Deserialize(bytes);

            Assert.Equal(WitnessStateTreeType.Binary, decoded.Features.StateTree);
            Assert.Equal(WitnessHashFunction.Blake3, decoded.Features.HashFunction);
            Assert.Equal(HardforkName.Osaka, decoded.Features.Fork);
            _output.WriteLine($"BinaryBlake3 witness: {bytes.Length} bytes, flags=0x{bytes[1]:X2}");
        }

        [Fact]
        public void RoundTrip_BinaryPoseidon()
        {
            var block = CreateBlock(BlockFeatureConfig.BinaryPoseidon());
            var bytes = BinaryBlockWitness.Serialize(block);
            var decoded = BinaryBlockWitness.Deserialize(bytes);

            Assert.Equal(WitnessStateTreeType.Binary, decoded.Features.StateTree);
            Assert.Equal(WitnessHashFunction.Poseidon, decoded.Features.HashFunction);
            _output.WriteLine($"BinaryPoseidon witness: {bytes.Length} bytes, flags=0x{bytes[1]:X2}");
        }

        [Fact]
        public void RoundTrip_BinarySha256()
        {
            var features = new BlockFeatureConfig
            {
                Fork = HardforkName.Osaka,
                StateTree = WitnessStateTreeType.Binary,
                HashFunction = WitnessHashFunction.Sha256
            };
            var block = CreateBlock(features);
            var bytes = BinaryBlockWitness.Serialize(block);
            var decoded = BinaryBlockWitness.Deserialize(bytes);

            Assert.Equal(WitnessStateTreeType.Binary, decoded.Features.StateTree);
            Assert.Equal(WitnessHashFunction.Sha256, decoded.Features.HashFunction);
        }

        [Fact]
        public void BackwardCompat_OldPatriciaWitness_DecodesCorrectly()
        {
            var block = CreateBlock(BlockFeatureConfig.Prague);
            var bytes = BinaryBlockWitness.Serialize(block);

            // Manually clear bits 3-5 to simulate a v1 witness that never set them
            bytes[1] = (byte)(bytes[1] & 0x07);

            var decoded = BinaryBlockWitness.Deserialize(bytes);
            Assert.Equal(WitnessStateTreeType.Patricia, decoded.Features.StateTree);
            Assert.Equal(WitnessHashFunction.Keccak, decoded.Features.HashFunction);
        }

        [Fact]
        public void FlagBits_EncodeCorrectly()
        {
            var block = CreateBlock(BlockFeatureConfig.BinaryBlake3());
            block.VerifyWitnessProofs = true;
            block.ComputePostStateRoot = true;
            block.ProduceBlockCommitments = true;

            var bytes = BinaryBlockWitness.Serialize(block);
            byte flags = bytes[1];

            Assert.True((flags & 0x01) != 0, "bit 0: VerifyWitnessProofs");
            Assert.True((flags & 0x02) != 0, "bit 1: ComputePostStateRoot");
            Assert.True((flags & 0x04) != 0, "bit 2: ProduceBlockCommitments");
            Assert.True((flags & 0x08) != 0, "bit 3: Binary state tree");
            Assert.Equal(1, (flags >> 4) & 0x03); // bits 4-5: Blake3 = 01

            _output.WriteLine($"All flags set: 0x{flags:X2} = 0b{System.Convert.ToString(flags, 2).PadLeft(8, '0')}");
        }

        private static BlockWitnessData CreateBlock(BlockFeatureConfig features)
        {
            return new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ComputePostStateRoot = true,
                Features = features,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(
                        CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 100000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 }, Storage = new List<WitnessStorageSlot>() }
                }
            };
        }
    }
}
