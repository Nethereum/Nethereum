using System.Collections.Generic;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// EIP-7843: <i>"slotNumber is a uint64 in big endian encoding."</i>
    ///
    /// <para>Covers the witness-to-context leg that <c>Eip7843SlotNumTests</c>
    /// explicitly does not: <see cref="BlockWitnessData.SlotNumber"/> reaching
    /// <c>TransactionExecutionContext.SlotNumber</c> through
    /// <c>TransactionContextFactory</c>, and from there the value SLOTNUM
    /// pushes. That conversion is where a signed carrier sign-extends, turning
    /// the top of the uint64 range into 2^256-1 with nothing to show for it.</para>
    /// </summary>
    public class Eip7843SlotNumberWidthTests
    {
        private const string SenderKey = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private static readonly string Sender = TestTransactionHelper.GetDefaultSenderAddress();
        private const string SlotnumContract = "0x2222222222222222222222222222222222222222";

        private static readonly byte[] SlotnumReturnRuntimeCode = "4B60005260206000F3".HexToByteArray();

        private static string SlotNumberPushedBySlotnum(ulong slotNumber)
        {
            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                BaseFee = 7,
                BlockGasLimit = 30_000_000,
                ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                SlotNumber = slotNumber,
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction>
                {
                    TestTransactionHelper.CreateSignedContractCall(
                        SlotnumContract, new byte[0], EvmUInt256.Zero, 0, 10, 100_000, SenderKey)
                },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount
                    {
                        Address = Sender,
                        Balance = new EvmUInt256(1_000_000_000_000_000_000UL),
                        Nonce = 0,
                        Code = new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = SlotnumContract,
                        Balance = EvmUInt256.Zero,
                        Nonce = 0,
                        Code = SlotnumReturnRuntimeCode,
                        Storage = new List<WitnessStorageSlot>()
                    }
                }
            };

            var result = Assert.Single(BlockExecutionHelper.ExecuteBlock(block).TxResults);
            Assert.True(result.Success, result.Error);
            return result.ReturnData.ToHex();
        }

        [Fact]
        public void Given_ASlotNumberAtMaxUint64_When_SLOTNUM_Executes_Then_ItPushesTwoToTheSixtyFourMinusOneZeroExtended()
        {
            Assert.Equal(
                "000000000000000000000000000000000000000000000000ffffffffffffffff",
                SlotNumberPushedBySlotnum(ulong.MaxValue));
        }

        [Fact]
        public void Given_AnOrdinarySlotNumber_When_SLOTNUM_Executes_Then_ItPushesThatValue()
        {
            Assert.Equal(
                "0000000000000000000000000000000000000000000000000000000000067932",
                SlotNumberPushedBySlotnum(424_242));
        }
    }
}
