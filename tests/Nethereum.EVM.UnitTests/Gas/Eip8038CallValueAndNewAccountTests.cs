using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8038CallValueAndNewAccountTests
    {
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";
        private const string CallerAddress = "0xABCDEF0123456789ABCDEF0123456789ABCDEF01";
        private const string OtherAddress = "0x9999999999999999999999999999999999999999";

        private static (Program program, ExecutionStateService stateService) CreateProgram(long gasRemaining = 1_000_000)
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callInput = new CallInput
            {
                To = ContractAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(gasRemaining),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.GasRemaining = gasRemaining;
            return (program, stateService);
        }

        private static void PushCallStack(Program program, string to, long value, long gas = 21000)
        {
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush((EvmUInt256)(ulong)value);
            program.StackPush(to.HexToByteArray());
            program.StackPush(gas);
        }

        private static void PushDelegateOrStaticCallStack(Program program, string to, long gas = 21000)
        {
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(to.HexToByteArray());
            program.StackPush(gas);
        }


        [Fact]
        public async Task Given_CallWithValue_AtAmsterdam_Then_ChargesElevenThousandThreeHundred_NotNineThousand()
        {
            var (programZero, stateZero) = CreateProgram();
            stateZero.CreditBalance(OtherAddress, (EvmUInt256)1);
            PushCallStack(programZero, OtherAddress, value: 0);
            var costZero = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, programZero);

            var (programValue, stateValue) = CreateProgram();
            stateValue.CreditBalance(OtherAddress, (EvmUInt256)1);
            PushCallStack(programValue, OtherAddress, value: 1);
            var costValue = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, programValue);

            var valueTransferDelta = costValue - costZero;

            Assert.Equal(GasConstants.EIP8038_CALL_VALUE_TRANSFER, valueTransferDelta);
            Assert.Equal(11300, valueTransferDelta);
            Assert.NotEqual(GasConstants.CALL_VALUE_TRANSFER, valueTransferDelta);
            Assert.NotEqual(9000, valueTransferDelta);
        }

        [Fact]
        public async Task Given_CallWithValue_AtAmsterdam_Then_ChildStillReceivesTheStipend()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            stateService.CreditBalance(callerAddress, (EvmUInt256)1_000_000_000_000);

            var calleeBytecode = "5A60005260206000F3".HexToByteArray();
            stateService.SaveCode(targetAddress, calleeBytecode);

            var callInput = new CallInput
            {
                To = callerAddress,
                From = callerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(1_000_000),
                Data = "0x"
            };
            var programContext = new ProgramContext(callInput, stateService, callerAddress);

            var callerBytecode = string.Concat(
                "6000",
                "6000",
                "6000",
                "6000",
                "6001",
                "73" + targetAddress.Substring(2),
                "6000",
                "F1",
                "00"
            ).HexToByteArray();

            var program = new Program(callerBytecode, programContext);
            program.GasRemaining = 1_000_000;

            var vm = new EVMSimulator(HardforkConfig.Amsterdam);
            await vm.ExecuteWithCallStackAsync(program, traceEnabled: false);

            Assert.True(program.Stopped);
            Assert.NotNull(program.ProgramResult.LastCallReturnData);
            var observedChildGas = new BigInteger(
                program.ProgramResult.LastCallReturnData.Reverse().Concat(new byte[] { 0 }).ToArray());

            Assert.Equal(GasConstants.CALL_STIPEND - 2, (long)observedChildGas);
            Assert.Equal(2298, (long)observedChildGas);
        }

        [Fact]
        public async Task Given_CallWithZeroValue_AtAmsterdam_Then_NoTransferCharge()
        {
            var (program, _) = CreateProgram();
            PushCallStack(program, OtherAddress, value: 0);

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, program);

            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, cost);
            Assert.Equal(3000, cost);
        }

        [Fact]
        public async Task Given_CallCodeWithValue_AtAmsterdam_Then_ChargesElevenThousandThreeHundred()
        {
            var (programZero, _) = CreateProgram();
            PushCallStack(programZero, OtherAddress, value: 0);
            var costZero = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALLCODE, programZero);

            var (programValue, _) = CreateProgram();
            PushCallStack(programValue, OtherAddress, value: 1);
            var costValue = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALLCODE, programValue);

            Assert.Equal(GasConstants.EIP8038_CALL_VALUE_TRANSFER, costValue - costZero);
            Assert.Equal(11300, costValue - costZero);
        }

        [Fact]
        public async Task Given_DelegateCallOrStaticCall_AtAmsterdam_Then_NoTransferChargeApplies()
        {
            var (delegateProgram, _) = CreateProgram();
            PushDelegateOrStaticCallStack(delegateProgram, OtherAddress);
            var delegateCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.DELEGATECALL, delegateProgram);

            var (staticProgram, _) = CreateProgram();
            PushDelegateOrStaticCallStack(staticProgram, OtherAddress);
            var staticCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.STATICCALL, staticProgram);

            var (osakaDelegateProgram, _) = CreateProgram();
            PushDelegateOrStaticCallStack(osakaDelegateProgram, OtherAddress);
            var osakaDelegateCost = await OpcodeHandlerSets.Osaka.GetGasCostAsync(Instruction.DELEGATECALL, osakaDelegateProgram);

            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, delegateCost);
            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, staticCost);
            Assert.Equal(osakaDelegateCost, delegateCost - (GasConstants.EIP8038_COLD_ACCOUNT_ACCESS - GasConstants.COLD_ACCOUNT_ACCESS_COST));
        }


        [Fact]
        public async Task Given_CallWithValueCreatingRecipient_AtAmsterdam_Then_ChargesNoExecutionNewAccountCost()
        {
            var (program, _) = CreateProgram();
            program.ProgramContext.StateGasActive = true;
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush((EvmUInt256)1);
            program.StackPush(OtherAddress.HexToByteArray());
            program.StackPush(21000);

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, program);

            var expectedExecutionOnly = GasConstants.EIP8038_COLD_ACCOUNT_ACCESS + GasConstants.EIP8038_CALL_VALUE_TRANSFER;
            Assert.Equal(expectedExecutionOnly, cost);
            Assert.Equal(14300, cost);
            Assert.NotEqual(expectedExecutionOnly + GasConstants.CALL_NEW_ACCOUNT, cost);
        }

        [Fact]
        public async Task Given_CallWithValueCreatingRecipient_AtAmsterdam_Then_ChargesNewAccountStateGas()
        {
            var (program, _) = CreateProgram();
            program.ProgramContext.StateGasActive = true;
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush((EvmUInt256)1);
            program.StackPush(OtherAddress.HexToByteArray());
            program.StackPush(21000);

            await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, program);

            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, program.StateGasSpilled);
            Assert.Equal(183600, program.StateGasSpilled);
        }

        [Fact]
        public async Task Given_CallWithValueToAnExistingAccount_AtAmsterdam_Then_ChargesNeitherNewAccountCost()
        {
            var (program, stateService) = CreateProgram();
            program.ProgramContext.StateGasActive = true;
            stateService.CreditBalance(OtherAddress, (EvmUInt256)1);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush((EvmUInt256)1);
            program.StackPush(OtherAddress.HexToByteArray());
            program.StackPush(21000);

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, program);

            var expected = GasConstants.EIP8038_COLD_ACCOUNT_ACCESS + GasConstants.EIP8038_CALL_VALUE_TRANSFER;
            Assert.Equal(expected, cost);
            Assert.Equal(0, program.StateGasSpilled);
        }

        [Fact]
        public async Task Given_SelfDestructSweepAtAmsterdam_Then_StillChargesBothAccountWriteAndStateGas()
        {
            var (program, stateService) = CreateProgram();
            program.ProgramContext.StateGasActive = true;
            stateService.CreditBalance(ContractAddress, (EvmUInt256)1);
            program.StackPush(OtherAddress.HexToByteArray());

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, program);

            var expectedExecution = GasConstants.SELFDESTRUCT_COST
                + GasConstants.EIP8038_COLD_ACCOUNT_ACCESS
                + GasConstants.EIP8038_ACCOUNT_WRITE;
            Assert.Equal(expectedExecution, cost);
            Assert.Equal(17000, cost);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, program.StateGasSpilled);
            Assert.Equal(183600, program.StateGasSpilled);
        }


        private static async Task<Program> RunCallerAsync(
            ExecutionStateService stateService, string callerAddress, byte[] callerBytecode,
            long gasRemaining = 1_000_000, int depth = 0,
            Nethereum.EVM.Execution.Precompiles.PrecompileRegistry precompiles = null)
        {
            var callInput = new CallInput
            {
                To = callerAddress,
                From = callerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(gasRemaining),
                Data = "0x"
            };
            var programContext = new ProgramContext(callInput, stateService, callerAddress)
            {
                StateGasActive = true
            };
            var program = new Program(callerBytecode, programContext);
            program.GasRemaining = gasRemaining;

            var config = precompiles != null
                ? HardforkConfig.Amsterdam.WithPrecompiles(precompiles)
                : HardforkConfig.Amsterdam;
            var vm = new EVMSimulator(config);
            await vm.ExecuteWithCallStackAsync(program, depth: depth, traceEnabled: false);
            return program;
        }

        private static byte[] ValueCallBytecode(string to, long value, long gas)
        {
            return string.Concat(
                "6000", "6000", "6000", "6000",
                PushHex(value),
                "73" + to.Substring(2),
                PushHex(gas),
                "F1",
                "00"
            ).HexToByteArray();
        }

        private static string PushHex(long value)
        {
            var hex = value.ToString("x");
            if (hex.Length % 2 != 0) hex = "0" + hex;
            var lenBytes = hex.Length / 2;
            var opcode = (0x60 + lenBytes - 1).ToString("x2");
            return opcode + hex;
        }

        [Fact]
        public async Task Given_ACallThatWouldCreateTheRecipient_When_TheDepthLimitAborts_Then_TheNewAccountStateGasIsCreditedBack()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callerAddress = "0x1111111111111111111111111111111111111111";
            var freshTarget = "0x3333333333333333333333333333333333333333";
            stateService.CreditBalance(callerAddress, (EvmUInt256)1_000_000_000_000);

            var bytecode = ValueCallBytecode(freshTarget, value: 1, gas: 0xFFFF);

            var program = await RunCallerAsync(stateService, callerAddress, bytecode, depth: GasConstants.MAX_CALL_DEPTH);

            Assert.True(program.Stopped);
            Assert.Equal(0, program.StateGasSpilled);
            Assert.Equal(0, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_ACallThatWouldCreateTheRecipient_When_TheCallerCannotFundIt_Then_TheNewAccountStateGasIsCreditedBack()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callerAddress = "0x1111111111111111111111111111111111111111";
            var freshTarget = "0x3333333333333333333333333333333333333333";

            var bytecode = ValueCallBytecode(freshTarget, value: 1, gas: 0xFFFF);

            var program = await RunCallerAsync(stateService, callerAddress, bytecode);

            Assert.True(program.Stopped);
            Assert.Equal(0, program.StateGasSpilled);
            Assert.Equal(0, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_ACallThatCreatedTheRecipient_When_TheChildReverts_Then_TheNewAccountStateGasIsCreditedBack()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callerAddress = "0x1111111111111111111111111111111111111111";
            const string sha256Precompile = "0x0000000000000000000000000000000000000002";
            stateService.CreditBalance(callerAddress, (EvmUInt256)1_000_000_000_000);

            var bytecode = ValueCallBytecode(sha256Precompile, value: 1, gas: 0);

            var program = await RunCallerAsync(
                stateService, callerAddress, bytecode,
                precompiles: Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            Assert.True(program.Stopped);
            Assert.Equal(0, program.StateGasSpilled);
            Assert.Equal(0, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_ACallThatCreatesTheRecipient_When_TheChildSucceeds_Then_TheStateGasIsKept()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callerAddress = "0x1111111111111111111111111111111111111111";
            var freshTarget = "0x3333333333333333333333333333333333333333";
            stateService.CreditBalance(callerAddress, (EvmUInt256)1_000_000_000_000);

            var bytecode = ValueCallBytecode(freshTarget, value: 1, gas: 0xFFFF);

            var program = await RunCallerAsync(stateService, callerAddress, bytecode);

            Assert.True(program.Stopped);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, program.StateGasSpilled);
            Assert.Equal(183600, program.StateGasSpilled);
            Assert.Equal(1, (int)program.StackPeekAtU256(0));
        }

        // satisfies just as well. EIP-8037 §Transaction-level gas
        // LIFO — "the charged state-gas is refilled in LIFO order", so

        private const string SpillingChildAddress = "0x5555555555555555555555555555555555555555";

        private static readonly byte[] StorageSetThenRevert =
            "600560015560006000FD".HexToByteArray();

        private static readonly byte[] StorageSetThenStop =
            "600560015500".HexToByteArray();

        private const long CallerStartGas = 1_000_000;

        [Fact]
        public async Task Given_AFrameThatSpilledStateGasIntoExecutionGas_When_ItReverts_Then_TheRefillRepaysExecutionGasBeforeTheReservoir()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callerAddress = "0x1111111111111111111111111111111111111111";
            stateService.SaveCode(SpillingChildAddress, StorageSetThenRevert);

            var bytecode = ValueCallBytecode(SpillingChildAddress, value: 0, gas: 500_000);

            var program = await RunCallerAsync(stateService, callerAddress, bytecode, gasRemaining: CallerStartGas);

            Assert.True(program.Stopped);
            Assert.Equal(0, (int)program.StackPeekAtU256(0));

            Assert.Equal(0, program.StateGasLeft);
            Assert.True(program.GasRemaining > CallerStartGas - GasConstants.EIP8037_STORAGE_SET_STATE_GAS,
                $"the reverting frame's spill was not repaid to execution gas: {CallerStartGas - program.GasRemaining} gas is gone against a {GasConstants.EIP8037_STORAGE_SET_STATE_GAS} state charge that spilled from it");
        }

        [Fact]
        public async Task Given_AFrameThatSpilledStateGasIntoExecutionGas_When_ItStops_Then_TheSpillSurvivesIntoTheCaller()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callerAddress = "0x1111111111111111111111111111111111111111";
            stateService.SaveCode(SpillingChildAddress, StorageSetThenStop);

            var bytecode = ValueCallBytecode(SpillingChildAddress, value: 0, gas: 500_000);

            var program = await RunCallerAsync(stateService, callerAddress, bytecode, gasRemaining: CallerStartGas);

            Assert.True(program.Stopped);
            Assert.Equal(1, (int)program.StackPeekAtU256(0));
            Assert.Equal(GasConstants.EIP8037_STORAGE_SET_STATE_GAS, program.StateGasSpilled);
            Assert.Equal(0, program.StateGasLeft);
        }
    }
}
