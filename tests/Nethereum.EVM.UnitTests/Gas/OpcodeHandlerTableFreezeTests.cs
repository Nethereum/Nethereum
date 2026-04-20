using System;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Gas.Opcodes.Costs;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class OpcodeHandlerTableFreezeTests
    {
        [Fact]
        public void NewTable_IsNotFrozen()
        {
            var t = new OpcodeHandlerTable();
            Assert.False(t.IsFrozen);
        }

        [Fact]
        public void Freeze_MarksTableAsFrozen()
        {
            var t = new OpcodeHandlerTable();
            t.Freeze();
            Assert.True(t.IsFrozen);
        }

        [Fact]
        public void Freeze_ReturnsSameInstance_ForChaining()
        {
            var t = new OpcodeHandlerTable();
            var returned = t.Freeze();
            Assert.Same(t, returned);
        }

        [Fact]
        public void Register_OnFrozenTable_Throws()
        {
            var t = new OpcodeHandlerTable().Freeze();
            Assert.Throws<InvalidOperationException>(
                () => t.RegisterGas(Instruction.ADD, FixedGasCost.G3));
        }

        [Fact]
        public void RegisterExec_OnFrozenTable_Throws()
        {
            var t = new OpcodeHandlerTable();
            t.RegisterGas(Instruction.ADD, FixedGasCost.G3);
            t.Freeze();
            Assert.Throws<InvalidOperationException>(
                () => t.RegisterExec(Instruction.ADD, null));
        }

        [Fact]
        public void AllRegisterOverloads_ThrowOnFrozenTable()
        {
            var t = new OpcodeHandlerTable().Freeze();

            Assert.Throws<InvalidOperationException>(
                () => t.Register(Instruction.ADD, FixedGasCost.G3, null));
            Assert.Throws<InvalidOperationException>(
                () => t.RegisterAsync(Instruction.SSTORE, null, null));
            Assert.Throws<InvalidOperationException>(
                () => t.RegisterGas(Instruction.ADD, FixedGasCost.G3));
            Assert.Throws<InvalidOperationException>(
                () => t.RegisterGasAsync(Instruction.SSTORE, null));
            Assert.Throws<InvalidOperationException>(
                () => t.RegisterExec(Instruction.ADD, null));
            Assert.Throws<InvalidOperationException>(
                () => t.RegisterExecAsync(Instruction.SSTORE, null));
        }

        [Fact]
        public void PerForkHardforkConfig_HasFrozenOpcodeTable()
        {
            // Every per-fork HardforkConfig built via HardforkConfig.Build must carry
            // a frozen OpcodeHandlerTable so nothing can mutate the singleton post-init.
            Assert.True(HardforkConfig.Frontier.OpcodeHandlers.IsFrozen);
            Assert.True(HardforkConfig.Cancun.OpcodeHandlers.IsFrozen);
            Assert.True(HardforkConfig.Prague.OpcodeHandlers.IsFrozen);
            Assert.True(HardforkConfig.Osaka.OpcodeHandlers.IsFrozen);
        }
    }
}
