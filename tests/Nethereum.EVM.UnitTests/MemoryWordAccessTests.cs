using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class MemoryWordAccessTests
    {
        private const string WordOneTwoThree = "7f0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";

        [Fact]
        public async Task Given_AValueWiderThanAByte_When_Mstore8WritesItAtTheLastByteOfAWord_Then_OnlyTheLowByteLandsAndMemoryIsOneWord()
        {
            var program = await Run("6101ff601f53");

            Assert.Equal(new string('0', 62) + "ff", program.GetCurrentMemoryAsHex());
        }

        [Fact]
        public async Task Given_AnOffsetPastTheFirstWord_When_Mstore8WritesThere_Then_MemoryGrowsByOneWholeWord()
        {
            var program = await Run("60aa602053");

            Assert.Equal(64, program.Memory.Count);
            Assert.Equal(0xaa, program.Memory[32]);
        }

        [Fact]
        public async Task Given_AWordStoredAtAnUnalignedOffset_When_ItIsLoadedBack_Then_TheSameWordIsOnTheStack()
        {
            var program = await Run(WordOneTwoThree + "600152" + "600151");

            Assert.Equal(64, program.Memory.Count);
            Assert.Equal(EvmUInt256.FromHex(WordOneTwoThree.Substring(2)), program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_AWordStoredAtAnUnalignedOffset_When_TheFirstWordIsLoaded_Then_TheLeadingZeroByteAndThe31HighBytesAreRead()
        {
            var program = await Run(WordOneTwoThree + "600152" + "600051");

            Assert.Equal(EvmUInt256.FromHex("00" + WordOneTwoThree.Substring(2, 62)), program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_AWordStoredTwice_When_TheSecondStoreStartsInsideTheFirst_Then_TheOverlappedByteHoldsTheSecondWordsHighByte()
        {
            var program = await Run("60ff600052" + "6001601f52");

            Assert.Equal(64, program.Memory.Count);
            Assert.Equal(0x00, program.Memory[31]);
            Assert.Equal(0x01, program.Memory[62]);
            Assert.All(program.Memory.GetRange(0, 31), value => Assert.Equal(0, value));
        }

        private static async Task<Program> Run(string bytecodeHex)
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());
            var transaction = new TransactionInput
            {
                From = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b",
                To = "0xcccccccccccccccccccccccccccccccccccccccc",
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(1000000),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(10),
                Data = "0x",
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(1)
            };
            var context = new ProgramContext(transaction, executionState, null,
                blockNumber: 1, timestamp: 1000, coinbase: "0x0000000000000000000000000000000000000000", baseFee: 10);
            var program = new Program(bytecodeHex.HexToByteArray(), context);
            return await new EVMSimulator(DefaultHardforkConfigs.Cancun).ExecuteWithCallStackAsync(program, traceEnabled: false);
        }
    }
}
