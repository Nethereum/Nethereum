using Nethereum.EVM.Execution.Opcodes.Executors;
using Nethereum.EVM.Execution.SelfDestruct;
using Nethereum.EVM.Execution.SelfDestruct.Rules;
using Nethereum.EVM.Gas.Opcodes.Costs;
using Nethereum.EVM.Gas.Opcodes.Rules;

namespace Nethereum.EVM.Execution.Opcodes
{
    public static partial class OpcodeHandlerSets
    {
        /// <summary>
        /// Self-contained Prague/Osaka base handler table. Registers all opcodes
        /// directly without calling any other fork builder. Use for Zisk/zkVM
        /// builds where only one fork config is needed and intermediate forks
        /// can be excluded from the binary.
        /// </summary>
        private static OpcodeHandlerTable BuildStandalone(bool hasClz)
        {
            var pe = new EvmProgramExecution();
            var eip2929Account = Eip2929AccessAccountRule.Instance;
            var eip2929Storage = Eip2929AccessStorageRule.Instance;

            var t = new OpcodeHandlerTable();

            // --- Gas: common fixed costs ---
            RegisterCommonGas(t, expByteCost: 50);

            // --- Gas: Berlin+ EIP-2929 dynamic costs ---
            t.RegisterGas(Instruction.BALANCE, new AccountAccessGasCost(eip2929Account));
            t.RegisterGas(Instruction.EXTCODESIZE, new AccountAccessGasCost(eip2929Account));
            t.RegisterGas(Instruction.EXTCODEHASH, new AccountAccessGasCost(eip2929Account));
            t.RegisterGas(Instruction.EXTCODECOPY, new ExtCodeCopyGasCost(eip2929Account));
            t.RegisterGas(Instruction.SLOAD, new SloadGasCost(eip2929Storage));
            t.RegisterGasAsync(Instruction.SSTORE, new SstoreGasCost(eip2929Storage));

            // --- Gas: CALL family (gas-only, execution in StepWithCallStack) ---
            t.RegisterGasAsync(Instruction.CALL, new CallGasCost(eip2929Account));
            t.RegisterGas(Instruction.CALLCODE, new CallCodeGasCost(eip2929Account));
            t.RegisterGas(Instruction.DELEGATECALL, new DelegateCallGasCost(eip2929Account));
            t.RegisterGas(Instruction.STATICCALL, new StaticCallGasCost(eip2929Account));

            // --- Gas: CREATE family ---
            t.RegisterGas(Instruction.CREATE, new CreateGasCost(hasInitCodeWordGas: true));
            t.RegisterGas(Instruction.CREATE2, new Create2GasCost(hasInitCodeWordGas: true));

            // --- Gas: post-Istanbul additions ---
            t.RegisterGas(Instruction.CHAINID, FixedGasCost.G2);
            t.RegisterGas(Instruction.SELFBALANCE, FixedGasCost.G5);
            t.RegisterGas(Instruction.BASEFEE, FixedGasCost.G2);
            t.RegisterGas(Instruction.PUSH0, FixedGasCost.G2);
            t.RegisterGas(Instruction.RETURNDATASIZE, FixedGasCost.G2);
            t.RegisterGas(Instruction.RETURNDATACOPY, CopyGasCost.Instance);
            t.RegisterGas(Instruction.REVERT, ReturnRevertGasCost.Instance);
            t.RegisterGas(Instruction.SHL, FixedGasCost.G3);
            t.RegisterGas(Instruction.SHR, FixedGasCost.G3);
            t.RegisterGas(Instruction.SAR, FixedGasCost.G3);

            // --- Gas: Cancun additions ---
            t.RegisterGas(Instruction.TLOAD, FixedGasCost.G100);
            t.RegisterGas(Instruction.TSTORE, FixedGasCost.G100);
            t.RegisterGas(Instruction.MCOPY, MCopyGasCost.Instance);
            t.RegisterGas(Instruction.BLOBHASH, FixedGasCost.G3);
            t.RegisterGas(Instruction.BLOBBASEFEE, FixedGasCost.G2);

            // --- Gas: Osaka additions ---
            if (hasClz)
                t.RegisterGas(Instruction.CLZ, FixedGasCost.G5);

            // --- Gas: SELFDESTRUCT ---
            t.RegisterGasAsync(Instruction.SELFDESTRUCT, new SelfDestructGasCost(hasColdWarmAccess: true));

            // --- Executors ---
            var arith = new ArithmeticBitwiseExecutor(pe.Arithmetic, pe.Bitwise, hasShifts: true, hasClz: hasClz);
            RegisterArithmeticBitwise(t, arith);
            t.RegisterExec(Instruction.SHL, arith);
            t.RegisterExec(Instruction.SHR, arith);
            t.RegisterExec(Instruction.SAR, arith);
            if (hasClz)
                t.RegisterExec(Instruction.CLZ, arith);

            var stack = new StackFlowExecutor(pe.StackFlowExecution, hasPush0: true);
            RegisterStackFlow(t, stack);
            t.RegisterExec(Instruction.PUSH0, stack);

            var mem = new MemoryExecutor(pe.StorageMemory, hasMCopy: true);
            RegisterMemory(t, mem);
            t.RegisterExec(Instruction.MCOPY, mem);

            var ctx = new ContextExecutor(pe.CallInput, pe.CallData, pe.Code, pe.BlockchainCurrentContractContext,
                hasChainId: true, hasBaseFee: true, hasBlobOps: true);
            RegisterContext(t, ctx);
            t.RegisterExec(Instruction.CHAINID, ctx);
            t.RegisterExec(Instruction.BASEFEE, ctx);
            t.RegisterExec(Instruction.BLOBHASH, ctx);
            t.RegisterExec(Instruction.BLOBBASEFEE, ctx);

            var logRet = new LogReturnExecutor(pe.ReturnRevertLogExecution, hasRevert: true, hasReturnData: true);
            RegisterLogReturn(t, logRet);
            t.RegisterExec(Instruction.RETURNDATASIZE, logRet);
            t.RegisterExec(Instruction.RETURNDATACOPY, logRet);
            t.RegisterExec(Instruction.REVERT, logRet);

            var balCode = new BalanceCodeExecutor(pe.BlockchainCurrentContractContext, pe.Code,
                hasSelfBalance: true, hasExtCodeHash: true);
            RegisterBalanceCode(t, balCode);
            t.RegisterExecAsync(Instruction.SELFBALANCE, balCode);
            t.RegisterExecAsync(Instruction.EXTCODEHASH, balCode);

            var blockHash = new BlockHashExecutor();
            RegisterBlockHash(t, blockHash);

            var storage = new StorageExecutor(pe.StorageMemory, pe.BlockchainCurrentContractContext,
                hasTransientStorage: true);
            RegisterStorage(t, storage);
            t.RegisterExecAsync(Instruction.TLOAD, storage);
            t.RegisterExecAsync(Instruction.TSTORE, storage);

            var selfDestruct = new SelfDestructExecutor(pe, Eip6780SelfDestructRule.Instance);
            t.RegisterExecAsync(Instruction.SELFDESTRUCT, selfDestruct);

            return t;
        }

        public static OpcodeHandlerTable PragueStandalone() => BuildStandalone(hasClz: false);

        public static OpcodeHandlerTable OsakaStandalone() => BuildStandalone(hasClz: true);
    }
}
