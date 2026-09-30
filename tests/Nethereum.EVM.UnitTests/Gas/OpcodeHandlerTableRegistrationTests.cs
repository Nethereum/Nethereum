using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Gas.Opcodes.Costs;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// A fork's table is the previous fork's table with handlers registered
    /// over it, so a registration has to supersede whatever priced the opcode
    /// before — including a handler of the other flavour. SELFDESTRUCT is the
    /// case that makes it observable: EIP-150 — "Increase the gas cost of
    /// SELFDESTRUCT to 5000 (from 0)."
    /// </summary>
    public class OpcodeHandlerTableRegistrationTests
    {
        private sealed class StubAsyncGasCost : IOpcodeGasCostAsync
        {
            private readonly long _cost;

            public StubAsyncGasCost(long cost) => _cost = cost;

            public Task<long> GetGasCostAsync(Program program) => Task.FromResult(_cost);
        }

        private sealed class StubExecutor : IOpcodeExecutor
        {
            private readonly bool _handled;

            public StubExecutor(bool handled) => _handled = handled;

            public bool Execute(Instruction opcode, Program program) => _handled;
        }

        private sealed class StubAsyncExecutor : IOpcodeExecutorAsync
        {
            private readonly bool _handled;

            public StubAsyncExecutor(bool handled) => _handled = handled;

            public Task<bool> ExecuteAsync(Instruction opcode, Program program) => Task.FromResult(_handled);
        }

        [Fact]
        public async Task Given_AnOpcodePricedAsynchronously_When_ASynchronousRuleIsRegisteredOverIt_Then_TheReplacementPrices()
        {
            var table = new OpcodeHandlerTable();
            table.RegisterGasAsync(Instruction.SELFDESTRUCT, new StubAsyncGasCost(5000));
            table.RegisterGas(Instruction.SELFDESTRUCT, FixedGasCost.Zero);

            Assert.Equal(0, await table.GetGasCostAsync(Instruction.SELFDESTRUCT, null));
        }

        [Fact]
        public async Task Given_AnOpcodePricedSynchronously_When_AnAsynchronousRuleIsRegisteredOverIt_Then_TheReplacementPrices()
        {
            var table = new OpcodeHandlerTable();
            table.RegisterGas(Instruction.SELFDESTRUCT, FixedGasCost.Zero);
            table.RegisterGasAsync(Instruction.SELFDESTRUCT, new StubAsyncGasCost(5000));

            Assert.Equal(5000, await table.GetGasCostAsync(Instruction.SELFDESTRUCT, null));
        }

        [Fact]
        public async Task Given_AnOpcodeExecutedAsynchronously_When_ASynchronousExecutorIsRegisteredOverIt_Then_TheReplacementRuns()
        {
            var table = new OpcodeHandlerTable();
            table.RegisterExecAsync(Instruction.SELFDESTRUCT, new StubAsyncExecutor(handled: true));
            table.RegisterExec(Instruction.SELFDESTRUCT, new StubExecutor(handled: false));

            Assert.False(await table.ExecuteAsync(Instruction.SELFDESTRUCT, null));
        }

        [Fact]
        public async Task Given_AnOpcodeExecutedSynchronously_When_AnAsynchronousExecutorIsRegisteredOverIt_Then_TheReplacementRuns()
        {
            var table = new OpcodeHandlerTable();
            table.RegisterExec(Instruction.SELFDESTRUCT, new StubExecutor(handled: false));
            table.RegisterExecAsync(Instruction.SELFDESTRUCT, new StubAsyncExecutor(handled: true));

            Assert.True(await table.ExecuteAsync(Instruction.SELFDESTRUCT, null));
        }

        [Fact]
        public void Given_TheFrontierTable_When_TangerineWhistleRegistersOverIt_Then_SelfDestructIsNoLongerFree()
        {
            Assert.Equal("SELFDESTRUCT: gas=FixedGasCost, exec=SelfDestructExecutor",
                HardforkConfig.Frontier.OpcodeHandlers.Describe(Instruction.SELFDESTRUCT));

            Assert.Equal("SELFDESTRUCT: gas=SelfDestructBeneficiaryResolvingGasCost, exec=SelfDestructExecutor",
                HardforkConfig.TangerineWhistle.OpcodeHandlers.Describe(Instruction.SELFDESTRUCT));
        }
    }
}
