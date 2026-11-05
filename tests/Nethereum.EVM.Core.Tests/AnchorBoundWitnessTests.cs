using System.Collections.Generic;
using Nethereum.EVM.Core.Tests.GeneralStateTests;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class AnchorBoundWitnessTests
    {
        private static readonly string Sender = TestTransactionHelper.GetDefaultSenderAddress();
        private const string SenderKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private const string Receiver = "0x1111111111111111111111111111111111111111";

        private static BlockWitnessData TransferBlock()
        {
            return new BlockWitnessData
            {
                BlockNumber = 1, Timestamp = 1000000, BaseFee = 7,
                BlockGasLimit = 30000000, ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32], ParentHash = new byte[32],
                ExtraData = new byte[0], MixHash = new byte[32], Nonce = new byte[8],
                Features = BlockFeatureConfig.Prague,
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedTransfer(Receiver, new EvmUInt256(1000), 0, 10, 21000, SenderKey)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount { Address = Sender, Balance = new EvmUInt256(1000000000000000000), Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() },
                    new WitnessAccount { Address = Receiver, Balance = EvmUInt256.Zero, Nonce = 0, Code = new byte[0], Storage = new List<WitnessStorageSlot>() }
                }
            }.AddRequestPredeploys();
        }

        private static byte[] SomeAnchorInputs() =>
            AnchorBinding.Encode(new List<EvmUInt256> { new EvmUInt256(1337), new EvmUInt256(1), new EvmUInt256(2), new EvmUInt256(7), new EvmUInt256(8), EvmUInt256.Zero });

        [Fact]
        public void Given_NoAnchorInputs_When_TheWitnessIsSerialised_Then_TheFlagBitIsClearAndTheBytesAreUnchangedByRoundTrip()
        {
            var bytes = BinaryBlockWitness.Serialize(TransferBlock());

            Assert.Equal(0, bytes[2] & 1);
            Assert.Null(BinaryBlockWitness.Deserialize(bytes).AnchorInputs);
            Assert.Equal(bytes, BinaryBlockWitness.Serialize(BinaryBlockWitness.Deserialize(bytes)));
        }

        [Fact]
        public void Given_AnchorInputs_When_TheWitnessIsSerialised_Then_TheyRoundTripAndOnlyTheFlagAndTheTrailerChange()
        {
            var plain = BinaryBlockWitness.Serialize(TransferBlock());
            var block = TransferBlock();
            block.AnchorInputs = SomeAnchorInputs();

            var anchored = BinaryBlockWitness.Serialize(block);

            Assert.Equal(1, anchored[2] & 1);
            Assert.Equal(block.AnchorInputs, BinaryBlockWitness.Deserialize(anchored).AnchorInputs);
            Assert.Equal(plain[0], anchored[0]);
            Assert.Equal(plain[1], anchored[1]);
            Assert.Equal(plain[3], anchored[3]);
        }

        [Fact]
        public void Given_SixWords_When_Encoded_Then_EachIsA32ByteBigEndianWordInOrder()
        {
            var encoded = SomeAnchorInputs();

            Assert.Equal(6 * 32, encoded.Length);
            Assert.Equal(1337, (encoded[30] << 8) | encoded[31]);
            Assert.Equal(2, encoded[3 * 32 - 1]);
        }

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void Given_AnchorInputs_When_TheGuestRuns_Then_ItCommitsKeccak256OfThemAndKeepsTheHostRoots()
        {
            var block = TransferBlock();
            block.ComputePostStateRoot = true;
            block.ProduceBlockCommitments = true;
            var host = BlockExecutionHelper.ExecuteBlock(block);
            block.AnchorInputs = SomeAnchorInputs();

            var guest = ZiskGuestRunner.Run(block);

            Assert.True(guest.Success);
            Assert.Equal(AnchorBinding.Commitment(block.AnchorInputs).ToHex(true), guest.AnchorCommitment);
            Assert.Equal(host.StateRoot.ToHex(true), guest.StateRoot);
            Assert.Equal(host.BlockHash.ToHex(true), guest.BlockHash);
        }
    }
}
