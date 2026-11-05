using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class StackReuseAcrossFramesTests
    {
        private const string FillsTheStack = "0x1111111111111111111111111111111111111111";
        private const string PopsAnEmptyStack = "0x2222222222222222222222222222222222222222";
        private const string OverflowsTheStack = "0x3333333333333333333333333333333333333333";
        private const string KeepsItsFirstValue = "0x4444444444444444444444444444444444444444";
        private const string Caller = "0xcccccccccccccccccccccccccccccccccccccccc";

        [Fact]
        public async Task Given_AChildThatFilledTheStackAndReturned_When_TheNextChildPopsAnEmptyStack_Then_ItHaltsWithStackUnderflow()
        {
            var program = await Run(StaticCallTo(FillsTheStack) + StaticCallTo(PopsAnEmptyStack));

            Assert.Equal(0, (int)program.StackPeekAtU256(0));
            Assert.Equal(1, (int)program.StackPeekAtU256(1));
        }

        [Fact]
        public async Task Given_AChildThatFilledTheStackAndReturned_When_TheNextChildPushesPastTheLimit_Then_ItHaltsWithStackOverflow()
        {
            var program = await Run(StaticCallTo(FillsTheStack) + StaticCallTo(OverflowsTheStack) + StaticCallTo(FillsTheStack));

            Assert.Equal(1, (int)program.StackPeekAtU256(0));
            Assert.Equal(0, (int)program.StackPeekAtU256(1));
            Assert.Equal(1, (int)program.StackPeekAtU256(2));
        }

        [Fact]
        public async Task Given_AChildWhoseStackGrowsPastItsFirstSlots_When_ItPopsBackToItsFirstValue_Then_TheValueSurvivedTheGrowth()
        {
            var program = await Run(
                "6020" + "6000" + "6000" + "6000" + "73" + KeepsItsFirstValue.Substring(2) + "5a" + "fa" + "50" + "6000" + "51");

            Assert.Equal(0x2a, (int)program.StackPeekAtU256(0));
        }

        private static string StaticCallTo(string address)
        {
            return "6000" + "6000" + "6000" + "6000" + "73" + address.Substring(2) + "5a" + "fa";
        }

        private static string Pops(int count)
        {
            return string.Concat(Enumerable.Repeat("50", count));
        }

        private static string Pushes(int count)
        {
            var code = new StringBuilder();
            for (int i = 0; i < count; i++) code.Append("6001");
            return code.ToString();
        }

        private static async Task<Program> Run(string callerCodeHex)
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());
            SetCode(executionState, FillsTheStack, Pushes(1024) + "00");
            SetCode(executionState, PopsAnEmptyStack, "5000");
            SetCode(executionState, OverflowsTheStack, Pushes(1025) + "00");
            SetCode(executionState, KeepsItsFirstValue, "602a" + Pushes(40) + Pops(40) + "6000" + "52" + "6020" + "6000" + "f3");

            var transaction = new TransactionInput
            {
                From = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b",
                To = Caller,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(10000000),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(10),
                Data = "0x",
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(1)
            };
            var context = new ProgramContext(transaction, executionState, null,
                blockNumber: 1, timestamp: 1000, coinbase: "0x0000000000000000000000000000000000000000", baseFee: 10);
            var program = new Program(callerCodeHex.HexToByteArray(), context);
            return await new EVMSimulator(DefaultHardforkConfigs.Cancun).ExecuteWithCallStackAsync(program, traceEnabled: false);
        }

        private static void SetCode(ExecutionStateService executionState, string address, string codeHex)
        {
            var account = executionState.CreateOrGetAccountExecutionState(address);
            account.Code = codeHex.HexToByteArray();
            account.Balance.SetInitialChainBalance(1);
        }
    }
}
