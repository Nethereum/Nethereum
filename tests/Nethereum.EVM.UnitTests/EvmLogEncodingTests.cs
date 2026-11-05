using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmLogEncodingTests
    {
        private const string Contract = "0x2222222222222222222222222222222222222222";
        private const string Word = "00112233445566778899aabbccddeeff0123456789abcdeffedcba9876543210";
        private const string SecondTopic = "00000000000000000000000000000000000000000000000000000000000000fe";
        private readonly EVMSimulator _vm = new EVMSimulator(DefaultHardforkConfigs.Cancun);

        [Fact]
        public async Task Given_a_LOG2_over_33_bytes_of_memory_When_executed_Then_the_log_carries_lowercase_hex_topics_and_data()
        {
            var code = "7f" + Word + "5f52" + "60ab6020" + "53" + "7f" + SecondTopic + "7f" + Word + "6021" + "5f" + "a2" + "00";

            var logs = await Execute(code);

            var log = Assert.Single(logs);
            Assert.Equal(Contract, log.Address);
            Assert.Equal(new[] { Word, SecondTopic }, log.Topics);
            Assert.Equal(Word + "ab", log.Data);
        }

        [Fact]
        public async Task Given_a_LOG1_of_zero_length_When_executed_Then_the_data_is_empty_and_the_topic_keeps_its_leading_zeros()
        {
            var code = "7f" + SecondTopic + "5f5fa100";

            var logs = await Execute(code);

            var log = Assert.Single(logs);
            Assert.Equal(new[] { SecondTopic }, log.Topics);
            Assert.Equal(string.Empty, log.Data);
        }

        [Fact]
        public async Task Given_a_LOG0_over_unwritten_memory_When_executed_Then_the_data_is_zero_bytes_of_the_requested_length()
        {
            var logs = await Execute("60205f" + "a000");

            var log = Assert.Single(logs);
            Assert.Empty(log.Topics);
            Assert.Equal(new string('0', 64), log.Data);
        }

        private async Task<System.Collections.Generic.List<FilterLog>> Execute(string hexBytecode)
        {
            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = Contract,
                Data = "0x",
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000)
            };
            var context = new ProgramContext(callInput, new ExecutionStateService(new MockNodeDataService()), "0x1111111111111111111111111111111111111111");
            var program = new Program(hexBytecode.HexToByteArray(), context) { GasRemaining = 1000000 };
            var executed = await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
            return executed.ProgramResult.Logs;
        }
    }
}
