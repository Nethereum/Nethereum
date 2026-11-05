using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmCallDataLoadTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator(DefaultHardforkConfigs.Cancun);

        [Theory]
        [InlineData("0102030405", 0, "0102030405000000000000000000000000000000000000000000000000000000")]
        [InlineData("0102030405", 3, "0405000000000000000000000000000000000000000000000000000000000000")]
        [InlineData("0102030405", 4, "0500000000000000000000000000000000000000000000000000000000000000")]
        [InlineData("0102030405", 5, "0000000000000000000000000000000000000000000000000000000000000000")]
        [InlineData("0102030405", 4294967296, "0000000000000000000000000000000000000000000000000000000000000000")]
        public async Task Given_calldata_shorter_than_a_word_When_CALLDATALOAD_Then_the_word_is_right_padded_with_zeros(string calldata, long offset, string expected)
        {
            var program = await Execute(calldata, "67" + offset.ToString("x16") + "35" + "00");

            Assert.Equal(expected, program.StackPeekAtU256(0).ToBigEndian().ToHex());
        }

        [Fact]
        public async Task Given_calldata_longer_than_a_word_When_CALLDATALOAD_at_an_inner_offset_Then_the_word_is_the_32_bytes_from_that_offset()
        {
            var calldata = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f2021222324252627";

            var program = await Execute(calldata, "600535" + "00");

            Assert.Equal("05060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f2021222324", program.StackPeekAtU256(0).ToBigEndian().ToHex());
        }

        private async Task<Program> Execute(string calldataHex, string bytecodeHex)
        {
            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Data = calldataHex,
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000)
            };
            var context = new ProgramContext(callInput, new ExecutionStateService(new MockNodeDataService()), "0x1111111111111111111111111111111111111111");
            var program = new Program(bytecodeHex.HexToByteArray(), context) { GasRemaining = 1000000 };
            return await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
        }
    }
}
