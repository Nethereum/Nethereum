using System.Collections.Generic;
using System.IO;
using Nethereum.EVM.Witness;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class GenerateTestWitness
    {
        private const string SENDER_KEY = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string SENDER = TestTransactionHelper.GetDefaultSenderAddress();
        private const string CONTRACT = "0x1000000000000000000000000000000000000000";

        [Fact]
        public void GenerateSimpleSstoreWitness()
        {
            var contractCode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };

            var block = new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                ProduceBlockCommitments = true, ComputePostStateRoot = true,
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(CONTRACT, new byte[0], EvmUInt256.Zero, 0, 10, 100000, SENDER_KEY)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = SENDER, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = CONTRACT, Balance = EvmUInt256.Zero, Nonce = 0, Code = contractCode, Storage = new List<WitnessStorageSlot>() }
                }
            };

            var bytes = BinaryBlockWitness.Serialize(block);
            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(GenerateTestWitness).Assembly.Location),
                "..", "..", "..", "..", "..", "zisk", "output", "test_sstore.bin");
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, bytes);

            Assert.True(bytes.Length > 0);
            Assert.Equal(1, bytes[0]);

            var deserialized = BinaryBlockWitness.Deserialize(bytes);
            Assert.Equal(block.Transactions[0].From, deserialized.Transactions[0].From);
            Assert.Equal(block.Accounts.Count, deserialized.Accounts.Count);
        }
    }
}
