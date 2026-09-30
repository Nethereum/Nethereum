using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class FrameHaltAndImplicitStopTraceTests
    {
        private const string Contract = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string Sender = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
        private const string Zero = "0x0000000000000000000000000000000000000000";

        private static byte[] StoreOneInSlotZero => "0x600160005500".HexToByteArray();

        private static (EVMSimulator Simulator, Program Program) Build(byte[] code, long gas, bool enforceSstoreGasStipend = false)
            => BuildWith(new ExecutionStateService(new MockNodeDataService()), code, gas, enforceSstoreGasStipend);

        private static ProgramTrace LastTrace(Program program) => program.Trace[program.Trace.Count - 1];

        [Fact]
        public async Task Given_AFrameThatRunsOutOfGas_When_TracingIsEnabled_Then_TheHaltTraceCarriesTheRequiredAndRemainingGasAndIsMarkedOutOfGas()
        {
            var (simulator, program) = Build(StoreOneInSlotZero, gas: 5000);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            Assert.True(program.ProgramResult.IsRevert);
            Assert.Equal(0, program.GasRemaining);

            var halt = LastTrace(program);
            Assert.True(halt.OutOfGas);
            Assert.True(halt.GasCost > halt.GasRemaining);
        }

        [Fact]
        public async Task Given_AFrameThatRunsOutOfGas_When_TracingIsDisabled_Then_NoHaltTraceIsAppended()
        {
            var (simulator, program) = Build(StoreOneInSlotZero, gas: 5000);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            Assert.True(program.ProgramResult.IsRevert);
            Assert.Equal(0, program.GasRemaining);
            Assert.Empty(program.Trace);
        }

        [Fact]
        public async Task Given_AnSstoreRefusedByTheStipendSentry_When_TracingIsEnabled_Then_TheHaltTraceChargesZeroGasAndReportsTheGasBeforeTheOpcode()
        {
            var (simulator, program) = Build(StoreOneInSlotZero, gas: 2000, enforceSstoreGasStipend: true);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            Assert.True(program.ProgramResult.IsRevert);

            var halt = LastTrace(program);
            Assert.True(halt.OutOfGas);
            Assert.Equal(0, halt.GasCost);
            Assert.True(halt.GasRemaining > 0);
            Assert.True(halt.GasRemaining <= GasConstants.SSTORE_GAS_STIPEND);
        }

        [Fact]
        public async Task Given_AnAffordableSstore_When_Traced_Then_TheTraceChargesTheOpcodesRealCost()
        {
            var (simulator, program) = Build(StoreOneInSlotZero, gas: 200000, enforceSstoreGasStipend: true);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            Assert.False(program.ProgramResult.IsRevert);

            var sstore = program.Trace.Find(t => t.Instruction?.Instruction == Instruction.SSTORE);
            Assert.NotNull(sstore);
            Assert.False(sstore.OutOfGas);
            Assert.True(sstore.GasCost > 0);
        }

        [Fact]
        public async Task Given_AFrameThatHaltsOnAnInvalidInstruction_When_TracingIsEnabled_Then_TheHaltTraceIsNotMarkedOutOfGas()
        {
            var (simulator, program) = Build("0x0c".HexToByteArray(), gas: 100000);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            Assert.True(program.ProgramResult.IsRevert);
            Assert.Equal(0, program.GasRemaining);

            var halt = LastTrace(program);
            Assert.False(halt.OutOfGas);
            Assert.True(halt.GasRemaining > 0);
        }

        [Fact]
        public async Task Given_ACallThatForwardsGas_When_Traced_Then_TheCallOpcodesTraceCostIncludesTheForwardedGas()
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());

            var callee = "0x0000000000000000000000000000000000000104";
            var calleeAccount = executionState.CreateOrGetAccountExecutionState(callee);
            calleeAccount.Code = "0x600160005260206000f3".HexToByteArray();
            calleeAccount.Balance.SetInitialChainBalance(1000000000);

            var code = "0x60206000600060006000630000010463001000f0f100".HexToByteArray();
            var (simulator, program) = BuildWith(executionState, code, gas: 10000000);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            var call = program.Trace.Find(t => t.Instruction?.Instruction == Instruction.CALL);
            Assert.NotNull(call);
            Assert.True(call.GasCost > 1000000);
        }

        [Fact]
        public async Task Given_ACreateThatForwardsGas_When_Traced_Then_TheCreateOpcodesTraceCostExcludesTheForwardedGas()
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());

            var code = "0x600060005260016000601ff000".HexToByteArray();
            var (simulator, program) = BuildWith(executionState, code, gas: 10000000);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            var create = program.Trace.Find(t => t.Instruction?.Instruction == Instruction.CREATE);
            Assert.NotNull(create);
            Assert.True(create.GasRemaining > 1000000);
            Assert.True(create.GasCost < 100000);
        }

        [Fact]
        public async Task Given_AProgramThatRunsOffTheEndOfItsBytecode_When_Traced_Then_AnImplicitStopTraceIsAppended()
        {
            var code = "0x6001".HexToByteArray();
            var (simulator, program) = Build(code, gas: 100000);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            Assert.True(program.StoppedImplicitly);

            var last = LastTrace(program);
            Assert.Equal(Instruction.STOP, last.Instruction?.Instruction);
            Assert.Equal(code.Length, last.Instruction.Step);
        }

        [Fact]
        public async Task Given_AProgramEndingInAnExplicitStop_When_Traced_Then_NoExtraImplicitStopTraceIsAppended()
        {
            var code = "0x600100".HexToByteArray();
            var (simulator, program) = Build(code, gas: 100000);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            Assert.False(program.StoppedImplicitly);
            Assert.Equal(1, program.Trace.FindAll(t => t.Instruction?.Instruction == Instruction.STOP).Count);
            Assert.Equal(2, LastTrace(program).Instruction.Step);
        }

        private static (EVMSimulator Simulator, Program Program) BuildWith(ExecutionStateService executionState, byte[] code, long gas, bool enforceSstoreGasStipend = false)
        {
            var account = executionState.CreateOrGetAccountExecutionState(Contract);
            account.Code = code;
            account.Balance.SetInitialChainBalance(1000000000);

            var transaction = new TransactionInput
            {
                From = Sender,
                To = Contract,
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(gas),
                GasPrice = new HexBigInteger(10),
                Data = "0x",
                ChainId = new HexBigInteger(1)
            };

            var programContext = new ProgramContext(transaction, executionState, null,
                blockNumber: 1, timestamp: 1000, coinbase: Zero, baseFee: 10)
            {
                EnforceSstoreGasStipend = enforceSstoreGasStipend
            };

            return (new EVMSimulator(DefaultHardforkConfigs.Cancun), new Program(code, programContext));
        }
    }
}
