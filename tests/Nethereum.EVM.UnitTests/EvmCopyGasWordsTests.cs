using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmCopyGasWordsTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator(DefaultHardforkConfigs.Cancun);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(31)]
        [InlineData(32)]
        [InlineData(33)]
        [InlineData(64)]
        [InlineData(1000)]
        [InlineData(1024)]
        [InlineData(4097)]
        public async Task Given_a_copy_of_a_length_When_CALLDATACOPY_runs_Then_the_gas_is_the_pushes_plus_the_base_the_per_word_charge_and_the_memory_expansion(int length)
        {
            var program = await Execute($"61{length:x4}5f5f37" + "00");

            long words = (length + 31) / 32;
            long expected = 3 + 2 + 2 + 3 + 3 * words + (3 * words + words * words / 512);
            Assert.Equal(expected, program.TotalGasUsed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(33)]
        [InlineData(1000)]
        public async Task Given_a_hashed_length_When_KECCAK256_runs_Then_the_gas_is_two_pushes_plus_the_base_the_per_word_charge_and_the_memory_expansion(int length)
        {
            var program = await Execute($"61{length:x4}5f20" + "00");

            long words = (length + 31) / 32;
            long expected = 3 + 2 + 30 + 6 * words + (3 * words + words * words / 512);
            Assert.Equal(expected, program.TotalGasUsed);
        }

        private async Task<Program> Execute(string bytecodeHex)
        {
            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Data = "0x",
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000)
            };
            var context = new ProgramContext(callInput, new ExecutionStateService(new MockNodeDataService()), "0x1111111111111111111111111111111111111111");
            var program = new Program(bytecodeHex.HexToByteArray(), context) { GasRemaining = 1000000 };
            return await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
        }
    }
}
