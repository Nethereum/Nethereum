using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Types;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public partial class HaltReasonParityTests
    {
        private const string Caller = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
        private const string Contract = "0xcccccccccccccccccccccccccccccccccccccccc";

        private const byte Push1 = 0x60;
        private const byte Pop = 0x50;
        private const byte Dup16 = 0x8f;
        private const byte Jump = 0x56;
        private const byte Sload = 0x54;
        private const byte Sstore = 0x55;
        private const byte Tload = 0x5c;
        private const byte Tstore = 0x5d;
        private const byte ReturnDataCopy = 0x3e;
        private const byte UndefinedByte = 0x0c;

        private static Program RunCode(byte[] code, long gas = 100_000, HardforkName fork = HardforkName.Osaka, bool isStatic = false, IStateReader reader = null)
        {
            var config = DefaultMainnetHardforkRegistry.Instance.Get(fork);
            var accounts = new Dictionary<string, AccountState>
            {
                [Caller] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000) },
                [Contract] = new AccountState { Code = code }
            };
            var executionState = new ExecutionStateService(reader ?? new InMemoryStateReader(accounts));
            var callContext = new EvmCallContext
            {
                From = Caller,
                To = Contract,
                Data = new byte[0],
                Value = EvmUInt256.Zero,
                Gas = gas,
                ChainId = EvmUInt256.One
            };
            var programContext = new ProgramContext(callContext, executionState,
                blockNumber: new EvmUInt256(1), timestamp: new EvmUInt256(1_000))
            {
                IsStatic = isStatic,
                EnforceSstoreGasStipend = config.EnforceSstoreGasStipend
            };
            var program = new Program(code, programContext);
            return Execute(new EVMSimulator(config), program);
        }

        private static void AssertHaltedWith(Program program, EvmHaltReason reason)
        {
            Assert.True(program.IsExceptionalHalt);
            Assert.Equal(reason, program.HaltReason);
            Assert.Equal(0, program.GasRemaining);
        }

        private static byte[] Pushes(int count) =>
            Enumerable.Repeat(new byte[] { Push1, 0x00 }, count).SelectMany(b => b).ToArray();

        [Fact]
        public void Given_an_undefined_opcode_byte_When_the_frame_executes_Then_it_halts_with_InvalidOpcode_consuming_all_gas()
            => AssertHaltedWith(RunCode(new byte[] { UndefinedByte }), EvmHaltReason.InvalidOpcode);

        [Fact]
        public void Given_TLOAD_at_Shanghai_where_it_is_not_defined_When_executed_Then_it_halts_with_InvalidOpcode()
            => AssertHaltedWith(RunCode(new byte[] { Push1, 0x00, Tload }, fork: HardforkName.Shanghai), EvmHaltReason.InvalidOpcode);

        [Fact]
        public void Given_TLOAD_at_Cancun_where_it_is_defined_When_executed_Then_the_frame_completes_without_a_halt()
        {
            var program = RunCode(new byte[] { Push1, 0x00, Tload }, fork: HardforkName.Cancun);
            Assert.False(program.IsExceptionalHalt);
            Assert.Equal(EvmHaltReason.None, program.HaltReason);
        }

        [Fact]
        public void Given_a_stack_of_1024_items_When_PUSH1_executes_Then_it_halts_with_StackOverflow()
            => AssertHaltedWith(RunCode(Pushes(1025)), EvmHaltReason.StackOverflow);

        [Fact]
        public void Given_a_stack_of_1023_items_When_PUSH1_executes_Then_the_frame_completes_with_1024_items()
        {
            var program = RunCode(Pushes(1024));
            Assert.False(program.IsExceptionalHalt);
            Assert.Equal(1024, program.GetCurrentStackAsHex().Count);
        }

        [Fact]
        public void Given_an_empty_stack_When_POP_executes_Then_it_halts_with_StackUnderflow()
            => AssertHaltedWith(RunCode(new byte[] { Pop }), EvmHaltReason.StackUnderflow);

        [Fact]
        public void Given_15_items_When_DUP16_executes_Then_it_halts_with_StackUnderflow()
            => AssertHaltedWith(RunCode(Pushes(15).Concat(new byte[] { Dup16 }).ToArray()), EvmHaltReason.StackUnderflow);

        [Fact]
        public void Given_an_empty_stack_When_SLOAD_is_priced_Then_it_halts_with_StackUnderflow_before_executing()
            => AssertHaltedWith(RunCode(new byte[] { Sload }), EvmHaltReason.StackUnderflow);

        [Fact]
        public void Given_a_JUMP_into_PUSH_data_When_executed_Then_it_halts_with_InvalidJumpDestination()
            => AssertHaltedWith(RunCode(new byte[] { Push1, 0x01, Jump }), EvmHaltReason.InvalidJumpDestination);

        [Fact]
        public void Given_a_static_frame_When_SSTORE_executes_Then_it_halts_with_WriteInStaticContext()
            => AssertHaltedWith(RunCode(new byte[] { Push1, 0x01, Push1, 0x00, Sstore }, isStatic: true), EvmHaltReason.WriteInStaticContext);

        [Fact]
        public void Given_a_static_frame_When_TSTORE_executes_Then_it_halts_with_WriteInStaticContext()
            => AssertHaltedWith(RunCode(new byte[] { Push1, 0x01, Push1, 0x00, Tstore }, isStatic: true), EvmHaltReason.WriteInStaticContext);

        [Fact]
        public void Given_gas_at_the_SSTORE_stipend_When_SSTORE_executes_Then_it_halts_with_OutOfGas()
            => AssertHaltedWith(RunCode(new byte[] { Push1, 0x01, Push1, 0x00, Sstore }, gas: 2306), EvmHaltReason.OutOfGas);

        [Fact]
        public void Given_less_gas_than_an_opcode_costs_When_it_is_charged_Then_it_halts_with_OutOfGas()
            => AssertHaltedWith(RunCode(new byte[] { Push1, 0x01, Push1, 0x01 }, gas: 5), EvmHaltReason.OutOfGas);

        [Fact]
        public void Given_RETURNDATACOPY_past_the_return_buffer_When_executed_Then_it_halts_with_OutOfBoundsRead()
            => AssertHaltedWith(RunCode(new byte[] { Push1, 0x01, Push1, 0x00, Push1, 0x00, ReturnDataCopy }), EvmHaltReason.OutOfBoundsRead);

        [Fact]
        public void Given_a_state_reader_that_throws_an_unexpected_exception_When_SLOAD_executes_Then_the_exception_surfaces_instead_of_halting_the_frame()
            => Assert.Throws<InvalidOperationException>(
                () => RunCode(new byte[] { Push1, 0x00, Sload }, reader: new ThrowingStateReader(new InvalidOperationException("reader bug"))));

        [Fact]
        public void Given_a_state_reader_that_throws_a_host_fault_When_SLOAD_executes_Then_the_host_fault_surfaces_unchanged()
            => Assert.Throws<EvmHostException>(
                () => RunCode(new byte[] { Push1, 0x00, Sload }, reader: new ThrowingStateReader(new EvmHostException("node unavailable"))));
    }
}
