using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    /// <summary>
    /// BDD coverage for EIP-8024 (Amsterdam): DUPN (0xE6), SWAPN (0xE7),
    /// EXCHANGE (0xE8). Rows AMS-8024-01..06/08 of
    /// <c>docs/internal/glamsterdam-traceability-matrix.md</c>.
    ///
    /// <para>
    /// Executes real bytecode through the production seam
    /// (<see cref="EVMSimulator.ExecuteWithCallStackAsync"/> over
    /// <see cref="HardforkConfig.Amsterdam"/>, which wires
    /// <see cref="OpcodeHandlerSets.Amsterdam"/>) rather than calling the
    /// stack-manipulation helpers directly, so the jump-destination
    /// analysis in <see cref="ProgramInstructionsUtils"/> is exercised
    /// exactly as a real transaction would hit it — that analysis is where
    /// the EIP's "conditional immediate absorption" rule lives and where a
    /// naive always-absorb implementation would be wrong (matrix
    /// AMS-8024-04: "the 07-15 instruction (unconditional absorption) was
    /// wrong").
    /// </para>
    /// </summary>
    public class Eip8024DupnSwapnExchangeTests
    {
        private static Program CreateProgram(byte[] bytecode, long gasRemaining = 1_000_000)
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);
            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Data = "",
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(gasRemaining)
            };
            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(bytecode, programContext);
            program.GasRemaining = gasRemaining;
            return program;
        }

        private static async Task<Program> RunAsync(Program program, HardforkConfig config)
        {
            var vm = new EVMSimulator(config);
            return await vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
        }

        private static List<byte> PushSequence(int count)
        {
            var code = new List<byte>();
            for (int v = 1; v <= count; v++)
            {
                code.Add((byte)Instruction.PUSH1);
                code.Add((byte)v);
            }
            return code;
        }


        [Fact]
        public async Task Given_DUPN_When_Executed_Then_DuplicatesDecodedDepth_AndAdvancesPcBy2()
        {
            var code = PushSequence(17);
            code.Add((byte)Instruction.DUPN);
            code.Add(0x80);

            var program = CreateProgram(code.ToArray());
            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.False(program.ProgramResult.IsRevert);
            Assert.Equal(1, (int)program.StackPeekAtU256(0));
            Assert.Equal(17, (int)program.StackPeekAtU256(1));
        }

        [Fact]
        public async Task Given_DUPN_Then_Costs3Gas()
        {
            var code = PushSequence(17);
            code.Add((byte)Instruction.DUPN);
            code.Add(0x80);
            var program = CreateProgram(code.ToArray());

            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.Equal(54, program.TotalGasUsed);
        }

        [Fact]
        public async Task Given_SWAPN_When_Executed_Then_SwapsTopWithDecodedDepth()
        {
            var code = PushSequence(18);
            code.Add((byte)Instruction.SWAPN);
            code.Add(0x80);

            var program = CreateProgram(code.ToArray());
            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.False(program.ProgramResult.IsRevert);
            Assert.Equal(1, (int)program.StackPeekAtU256(0));
            Assert.Equal(18, (int)program.StackPeekAtU256(17));
        }

        [Fact]
        public async Task Given_EXCHANGE_When_Executed_Then_SwapsDecodedPair_LeavingTopUntouched()
        {
            var code = PushSequence(17);
            code.Add((byte)Instruction.EXCHANGE);
            code.Add(0x80);

            var program = CreateProgram(code.ToArray());
            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.False(program.ProgramResult.IsRevert);
            Assert.Equal(17, (int)program.StackPeekAtU256(0));
            Assert.Equal(1, (int)program.StackPeekAtU256(1));
            Assert.Equal(16, (int)program.StackPeekAtU256(16));
        }


        [Fact]
        public async Task Given_DUPN_WithInsufficientStack_Then_Underflows_AndReverts()
        {
            var code = PushSequence(5);
            code.Add((byte)Instruction.DUPN);
            code.Add(0x80);

            var program = CreateProgram(code.ToArray());
            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
            Assert.Equal(0, program.GasRemaining);
        }

        [Fact]
        public async Task Given_SWAPN_WithInsufficientStack_Then_Underflows_AndReverts()
        {
            var code = PushSequence(5);
            code.Add((byte)Instruction.SWAPN);
            code.Add(0x80);

            var program = CreateProgram(code.ToArray());
            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }

        [Fact]
        public async Task Given_EXCHANGE_WithInsufficientStack_Then_Underflows_AndReverts()
        {
            var code = PushSequence(5);
            code.Add((byte)Instruction.EXCHANGE);
            code.Add(0x80);

            var program = CreateProgram(code.ToArray());
            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }


        [Fact]
        public async Task Given_ImmediateInForbiddenRange91To127_When_DUPN_Then_Halts()
        {
            var code = new byte[] { (byte)Instruction.DUPN, 0x60, (byte)Instruction.STOP };
            var program = CreateProgram(code);

            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }

        [Fact]
        public async Task Given_ExchangeImmediateInForbiddenRange82To127_Then_Halts()
        {
            var code = new byte[] { (byte)Instruction.EXCHANGE, 0x60, (byte)Instruction.STOP };
            var program = CreateProgram(code);

            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }


        [Fact]
        public async Task Given_ImmediateMissingAtEndOfCode_When_Executed_Then_HaltsWithoutBufferOverrun()
        {
            var code = new byte[] { (byte)Instruction.DUPN };
            var program = CreateProgram(code);

            var ex = await Record.ExceptionAsync(() => RunAsync(program, HardforkConfig.Amsterdam));

            Assert.Null(ex);
            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }


        [Fact]
        public async Task Given_ForbiddenRangeByteIs0x5B_When_JumpTargetsIt_Then_Succeeds()
        {
            var code = new byte[]
            {
                (byte)Instruction.PUSH1, 0x04,
                (byte)Instruction.JUMP,
                (byte)Instruction.DUPN,
                (byte)Instruction.JUMPDEST,
                (byte)Instruction.STOP
            };
            var program = CreateProgram(code);

            await RunAsync(program, HardforkConfig.Amsterdam);

            Assert.True(program.Stopped);
            Assert.False(program.ProgramResult.IsRevert);
        }

        [Fact]
        public async Task Given_ValidImmediateByte_When_JumpTargetsIt_Then_Fails()
        {
            var code = new byte[]
            {
                (byte)Instruction.PUSH1, 0x04,
                (byte)Instruction.JUMP,
                (byte)Instruction.DUPN,
                0x00,
                (byte)Instruction.JUMPDEST,
                (byte)Instruction.STOP
            };
            var program = CreateProgram(code);

            var ex = await Record.ExceptionAsync(() => RunAsync(program, HardforkConfig.Amsterdam));

            Assert.Null(ex);
            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }


        [Fact]
        public async Task Given_ForbiddenRangeByteIs0x5B_AtOsaka_When_JumpTargetsIt_Then_StillSucceeds()
        {
            var code = new byte[]
            {
                (byte)Instruction.PUSH1, 0x04,
                (byte)Instruction.JUMP,
                (byte)Instruction.DUPN,
                (byte)Instruction.JUMPDEST,
                (byte)Instruction.STOP
            };
            var program = CreateProgram(code);

            await RunAsync(program, HardforkConfig.Osaka);

            Assert.True(program.Stopped);
            Assert.False(program.ProgramResult.IsRevert);
        }

        [Fact]
        public void Given_OsakaOpcodeTable_Then_DoesNotRegisterEip8024Opcodes()
        {
            Assert.False(OpcodeHandlerSets.Osaka.IsRegistered(Instruction.DUPN));
            Assert.False(OpcodeHandlerSets.Osaka.IsRegistered(Instruction.SWAPN));
            Assert.False(OpcodeHandlerSets.Osaka.IsRegistered(Instruction.EXCHANGE));
        }

        [Fact]
        public void Given_AmsterdamOpcodeTable_Then_RegistersEip8024Opcodes()
        {
            Assert.True(OpcodeHandlerSets.Amsterdam.IsRegistered(Instruction.DUPN));
            Assert.True(OpcodeHandlerSets.Amsterdam.IsRegistered(Instruction.SWAPN));
            Assert.True(OpcodeHandlerSets.Amsterdam.IsRegistered(Instruction.EXCHANGE));
        }
    }
}
