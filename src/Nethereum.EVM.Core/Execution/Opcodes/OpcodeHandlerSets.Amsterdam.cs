using Nethereum.EVM.Execution.Opcodes.Executors;
using Nethereum.EVM.Execution.SelfDestruct;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Opcodes.Costs;
using Nethereum.EVM.Gas.Opcodes.Rules;

namespace Nethereum.EVM.Execution.Opcodes
{
    public static partial class OpcodeHandlerSets
    {
        private static void RegisterAmsterdamDeltas(
            OpcodeHandlerTable t,
            EvmProgramExecution pe,
            ContextExecutor context)
        {
            var account = _eip8038Account;

            t.RegisterGasAsync(Instruction.SELFDESTRUCT,
                new SelfDestructBeneficiaryResolvingGasCost(Eip161SelfDestructBeneficiaryRule.AsReboundByEip8038()));

            t.RegisterGas(Instruction.BALANCE, new AccountAccessGasCost(account));
            t.RegisterGas(Instruction.EXTCODEHASH, new AccountAccessGasCost(account));

            t.RegisterGas(Instruction.EXTCODESIZE, new AccountAccessGasCost(account, GasConstants.WARM_STORAGE_READ_COST));
            t.RegisterGas(Instruction.EXTCODECOPY, new ExtCodeCopyGasCost(account, GasConstants.WARM_STORAGE_READ_COST));

            t.RegisterGasAsync(Instruction.CALL, new CallTargetResolvingGasCost(
                account, Eip161CallNewAccountRule.AsReboundByEip8038()));
            t.RegisterGas(Instruction.CALLCODE, new CallCodeGasCost(
                account,
                valueTransferCost: GasConstants.EIP8038_CALL_VALUE_TRANSFER));
            t.RegisterGas(Instruction.DELEGATECALL, new DelegateCallGasCost(account));
            t.RegisterGas(Instruction.STATICCALL, new StaticCallGasCost(account));

            t.RegisterGas(Instruction.CREATE, new CreateGasCost(hasInitCodeWordGas: true, baseCost: GasConstants.EIP8038_CREATE_ACCESS));
            t.RegisterGas(Instruction.CREATE2, new Create2GasCost(hasInitCodeWordGas: true, baseCost: GasConstants.EIP8038_CREATE_ACCESS));

            t.RegisterGasAsync(Instruction.SSTORE, new SstoreSlotResolvingGasCost(Eip8038SstoreGasRule.Instance));

            t.RegisterGas(Instruction.DUPN, FixedGasCost.G3);
            t.RegisterGas(Instruction.SWAPN, FixedGasCost.G3);
            t.RegisterGas(Instruction.EXCHANGE, FixedGasCost.G3);
            var stack = new StackFlowExecutor(pe.StackFlowExecution, hasPush0: true);
            t.RegisterExec(Instruction.DUPN, stack);
            t.RegisterExec(Instruction.SWAPN, stack);
            t.RegisterExec(Instruction.EXCHANGE, stack);

            t.RegisterGas(Instruction.SLOTNUM, FixedGasCost.G2);
            t.RegisterExec(Instruction.SLOTNUM, context);

            t.RegisterExecAsync(Instruction.SELFDESTRUCT,
                new SelfDestructExecutor(pe, SelfDestructRuleSets.Amsterdam));
        }
    }
}
