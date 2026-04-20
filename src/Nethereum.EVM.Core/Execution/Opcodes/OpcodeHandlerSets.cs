using Nethereum.EVM.Execution.Opcodes.Executors;
using Nethereum.EVM.Execution.SelfDestruct;
using Nethereum.EVM.Gas.Opcodes.Costs;
using Nethereum.EVM.Gas.Opcodes.Rules;

namespace Nethereum.EVM.Execution.Opcodes
{
    public static partial class OpcodeHandlerSets
    {
        private static readonly EvmProgramExecution _pe = new EvmProgramExecution();
        private static readonly IAccessAccountRule _eip2929Account = Eip2929AccessAccountRule.Instance;
        private static readonly IAccessStorageRule _eip2929Storage = Eip2929AccessStorageRule.Instance;

        // ---------------------------------------------------------------
        // Helpers: register executor groups for their opcodes
        // ---------------------------------------------------------------

        private static void RegisterArithmeticBitwise(OpcodeHandlerTable t, ArithmeticBitwiseExecutor exec)
        {
            // Arithmetic
            t.RegisterExec(Instruction.ADD, exec);
            t.RegisterExec(Instruction.MUL, exec);
            t.RegisterExec(Instruction.SUB, exec);
            t.RegisterExec(Instruction.DIV, exec);
            t.RegisterExec(Instruction.SDIV, exec);
            t.RegisterExec(Instruction.MOD, exec);
            t.RegisterExec(Instruction.SMOD, exec);
            t.RegisterExec(Instruction.ADDMOD, exec);
            t.RegisterExec(Instruction.MULMOD, exec);
            t.RegisterExec(Instruction.EXP, exec);
            t.RegisterExec(Instruction.SIGNEXTEND, exec);
            // Comparison
            t.RegisterExec(Instruction.LT, exec);
            t.RegisterExec(Instruction.GT, exec);
            t.RegisterExec(Instruction.SLT, exec);
            t.RegisterExec(Instruction.SGT, exec);
            t.RegisterExec(Instruction.EQ, exec);
            t.RegisterExec(Instruction.ISZERO, exec);
            // Bitwise
            t.RegisterExec(Instruction.AND, exec);
            t.RegisterExec(Instruction.OR, exec);
            t.RegisterExec(Instruction.XOR, exec);
            t.RegisterExec(Instruction.NOT, exec);
            t.RegisterExec(Instruction.BYTE, exec);
        }

        private static void RegisterStackFlow(OpcodeHandlerTable t, StackFlowExecutor exec)
        {
            t.RegisterExec(Instruction.STOP, exec);
            t.RegisterExec(Instruction.POP, exec);
            t.RegisterExec(Instruction.JUMP, exec);
            t.RegisterExec(Instruction.JUMPI, exec);
            t.RegisterExec(Instruction.PC, exec);
            t.RegisterExec(Instruction.JUMPDEST, exec);
            t.RegisterExec(Instruction.INVALID, exec);
            for (int i = (int)Instruction.PUSH1; i <= (int)Instruction.PUSH32; i++)
                t.RegisterExec((Instruction)i, exec);
            for (int i = (int)Instruction.DUP1; i <= (int)Instruction.DUP16; i++)
                t.RegisterExec((Instruction)i, exec);
            for (int i = (int)Instruction.SWAP1; i <= (int)Instruction.SWAP16; i++)
                t.RegisterExec((Instruction)i, exec);
        }

        private static void RegisterMemory(OpcodeHandlerTable t, MemoryExecutor exec)
        {
            t.RegisterExec(Instruction.MLOAD, exec);
            t.RegisterExec(Instruction.MSTORE, exec);
            t.RegisterExec(Instruction.MSTORE8, exec);
            t.RegisterExec(Instruction.MSIZE, exec);
        }

        private static void RegisterContext(OpcodeHandlerTable t, ContextExecutor exec)
        {
            t.RegisterExec(Instruction.ADDRESS, exec);
            t.RegisterExec(Instruction.ORIGIN, exec);
            t.RegisterExec(Instruction.CALLER, exec);
            t.RegisterExec(Instruction.CALLVALUE, exec);
            t.RegisterExec(Instruction.CALLDATALOAD, exec);
            t.RegisterExec(Instruction.CALLDATASIZE, exec);
            t.RegisterExec(Instruction.CALLDATACOPY, exec);
            t.RegisterExec(Instruction.CODESIZE, exec);
            t.RegisterExec(Instruction.CODECOPY, exec);
            t.RegisterExec(Instruction.GASPRICE, exec);
            t.RegisterExec(Instruction.KECCAK256, exec);
            t.RegisterExec(Instruction.COINBASE, exec);
            t.RegisterExec(Instruction.TIMESTAMP, exec);
            t.RegisterExec(Instruction.NUMBER, exec);
            t.RegisterExec(Instruction.DIFFICULTY, exec);
            t.RegisterExec(Instruction.GASLIMIT, exec);
            t.RegisterExec(Instruction.GAS, exec);
        }

        private static void RegisterLogReturn(OpcodeHandlerTable t, LogReturnExecutor exec)
        {
            t.RegisterExec(Instruction.RETURN, exec);
            for (int i = 0; i <= 4; i++)
                t.RegisterExec((Instruction)((int)Instruction.LOG0 + i), exec);
        }

        private static void RegisterBalanceCode(OpcodeHandlerTable t, BalanceCodeExecutor exec)
        {
            t.RegisterExecAsync(Instruction.BALANCE, exec);
            t.RegisterExecAsync(Instruction.EXTCODESIZE, exec);
            t.RegisterExecAsync(Instruction.EXTCODECOPY, exec);
        }

        private static void RegisterStorage(OpcodeHandlerTable t, StorageExecutor exec)
        {
            t.RegisterExecAsync(Instruction.SLOAD, exec);
            t.RegisterExecAsync(Instruction.SSTORE, exec);
        }

        private static void RegisterBlockHash(OpcodeHandlerTable t, BlockHashExecutor exec)
        {
            t.RegisterExecAsync(Instruction.BLOCKHASH, exec);
        }

        // ---------------------------------------------------------------
        // Gas helpers (same as OpcodeGasCostTables)
        // ---------------------------------------------------------------

        private static void RegisterCommonGas(OpcodeHandlerTable t, int expByteCost)
        {
            // Arithmetic
            t.RegisterGas(Instruction.STOP, FixedGasCost.Zero);
            t.RegisterGas(Instruction.ADD, FixedGasCost.G3);
            t.RegisterGas(Instruction.MUL, FixedGasCost.G5);
            t.RegisterGas(Instruction.SUB, FixedGasCost.G3);
            t.RegisterGas(Instruction.DIV, FixedGasCost.G5);
            t.RegisterGas(Instruction.SDIV, FixedGasCost.G5);
            t.RegisterGas(Instruction.MOD, FixedGasCost.G5);
            t.RegisterGas(Instruction.SMOD, FixedGasCost.G5);
            t.RegisterGas(Instruction.ADDMOD, FixedGasCost.G8);
            t.RegisterGas(Instruction.MULMOD, FixedGasCost.G8);
            t.RegisterGas(Instruction.SIGNEXTEND, FixedGasCost.G5);
            t.RegisterGas(Instruction.LT, FixedGasCost.G3);
            t.RegisterGas(Instruction.GT, FixedGasCost.G3);
            t.RegisterGas(Instruction.SLT, FixedGasCost.G3);
            t.RegisterGas(Instruction.SGT, FixedGasCost.G3);
            t.RegisterGas(Instruction.EQ, FixedGasCost.G3);
            t.RegisterGas(Instruction.ISZERO, FixedGasCost.G3);
            t.RegisterGas(Instruction.AND, FixedGasCost.G3);
            t.RegisterGas(Instruction.OR, FixedGasCost.G3);
            t.RegisterGas(Instruction.XOR, FixedGasCost.G3);
            t.RegisterGas(Instruction.NOT, FixedGasCost.G3);
            t.RegisterGas(Instruction.BYTE, FixedGasCost.G3);
            // Context
            t.RegisterGas(Instruction.ADDRESS, FixedGasCost.G2);
            t.RegisterGas(Instruction.ORIGIN, FixedGasCost.G2);
            t.RegisterGas(Instruction.CALLER, FixedGasCost.G2);
            t.RegisterGas(Instruction.CALLVALUE, FixedGasCost.G2);
            t.RegisterGas(Instruction.CALLDATALOAD, FixedGasCost.G3);
            t.RegisterGas(Instruction.CALLDATASIZE, FixedGasCost.G2);
            t.RegisterGas(Instruction.CODESIZE, FixedGasCost.G2);
            t.RegisterGas(Instruction.GASPRICE, FixedGasCost.G2);
            t.RegisterGas(Instruction.BLOCKHASH, FixedGasCost.G20);
            t.RegisterGas(Instruction.COINBASE, FixedGasCost.G2);
            t.RegisterGas(Instruction.TIMESTAMP, FixedGasCost.G2);
            t.RegisterGas(Instruction.NUMBER, FixedGasCost.G2);
            t.RegisterGas(Instruction.DIFFICULTY, FixedGasCost.G2);
            t.RegisterGas(Instruction.GASLIMIT, FixedGasCost.G2);
            // Flow
            t.RegisterGas(Instruction.POP, FixedGasCost.G2);
            t.RegisterGas(Instruction.JUMP, FixedGasCost.G8);
            t.RegisterGas(Instruction.JUMPI, FixedGasCost.G10);
            t.RegisterGas(Instruction.PC, FixedGasCost.G2);
            t.RegisterGas(Instruction.MSIZE, FixedGasCost.G2);
            t.RegisterGas(Instruction.GAS, FixedGasCost.G2);
            t.RegisterGas(Instruction.JUMPDEST, FixedGasCost.G1);
            t.RegisterGas(Instruction.INVALID, FixedGasCost.Zero);
            // Dynamic
            t.RegisterGas(Instruction.EXP, new ExpGasCost(expByteCost));
            t.RegisterGas(Instruction.KECCAK256, Keccak256GasCost.Instance);
            t.RegisterGas(Instruction.MLOAD, MemoryLoadStoreGasCost.Instance);
            t.RegisterGas(Instruction.MSTORE, MemoryLoadStoreGasCost.Instance);
            t.RegisterGas(Instruction.MSTORE8, MemoryStore8GasCost.Instance);
            t.RegisterGas(Instruction.RETURN, ReturnRevertGasCost.Instance);
            t.RegisterGas(Instruction.CALLDATACOPY, CopyGasCost.Instance);
            t.RegisterGas(Instruction.CODECOPY, CopyGasCost.Instance);
            for (int i = 0; i <= 4; i++)
                t.RegisterGas((Instruction)((int)Instruction.LOG0 + i), new LogGasCost(i));
            // PUSH/DUP/SWAP
            for (int i = (int)Instruction.PUSH1; i <= (int)Instruction.PUSH32; i++)
                t.RegisterGas((Instruction)i, FixedGasCost.G3);
            for (int i = (int)Instruction.DUP1; i <= (int)Instruction.DUP16; i++)
                t.RegisterGas((Instruction)i, FixedGasCost.G3);
            for (int i = (int)Instruction.SWAP1; i <= (int)Instruction.SWAP16; i++)
                t.RegisterGas((Instruction)i, FixedGasCost.G3);
        }

        // ---------------------------------------------------------------
        // Per-fork handler tables — exposed as frozen static readonly
        // singletons so every consumer shares one immutable instance per
        // fork. HardforkConfig.Build (re-)freezes the reference it hands
        // out, so even direct consumers of these fields cannot mutate
        // them post-construction (Register* throws InvalidOperationException
        // on frozen tables). Build* helpers remain private and return
        // fresh mutable tables so the incremental fork chain
        // (Osaka inherits Prague inherits Cancun ...) can layer changes
        // without mutating a shared parent.
        // ---------------------------------------------------------------

        public static readonly OpcodeHandlerTable Frontier = BuildFrontier().Freeze();
        public static readonly OpcodeHandlerTable Homestead = Frontier;
        public static readonly OpcodeHandlerTable TangerineWhistle = BuildTangerineWhistle().Freeze();
        public static readonly OpcodeHandlerTable SpuriousDragon = BuildSpuriousDragon().Freeze();
        public static readonly OpcodeHandlerTable Byzantium = BuildByzantium().Freeze();
        public static readonly OpcodeHandlerTable Constantinople = BuildConstantinople().Freeze();
        public static readonly OpcodeHandlerTable Petersburg = Constantinople;
        public static readonly OpcodeHandlerTable Istanbul = BuildIstanbul().Freeze();
        public static readonly OpcodeHandlerTable Berlin = BuildBerlin().Freeze();
        public static readonly OpcodeHandlerTable London = BuildLondon().Freeze();
        public static readonly OpcodeHandlerTable Paris = London;
        public static readonly OpcodeHandlerTable Shanghai = BuildShanghai().Freeze();
        public static readonly OpcodeHandlerTable Cancun = BuildCancun().Freeze();
        public static readonly OpcodeHandlerTable Prague = Cancun;
        public static readonly OpcodeHandlerTable Osaka = BuildOsaka().Freeze();

        // ---------------------------------------------------------------
        // Private builders — return fresh mutable tables. Each non-alias
        // fork calls its parent builder (not the frozen field) so layered
        // mutations stay local to one chain.
        // ---------------------------------------------------------------

        private static OpcodeHandlerTable BuildFrontier()
        {
            var t = new OpcodeHandlerTable();

            // Gas
            RegisterCommonGas(t, expByteCost: 10);
            t.RegisterGas(Instruction.BALANCE, new FixedGasCost(20));
            t.RegisterGas(Instruction.EXTCODESIZE, new FixedGasCost(20));
            t.RegisterGas(Instruction.EXTCODECOPY, new ExtCodeCopyGasCost(fixedAccessCost: 20));
            t.RegisterGas(Instruction.SLOAD, new FixedGasCost(50));
            t.RegisterGas(Instruction.CREATE, new CreateGasCost(hasInitCodeWordGas: false));
            t.RegisterGasAsync(Instruction.SSTORE, new SstoreFrontierGasCost());
            t.RegisterGasAsync(Instruction.CALL, new CallGasCost(fixedAccessCost: 40, newAccountRequiresValue: false));
            t.RegisterGas(Instruction.CALLCODE, new CallCodeGasCost(fixedAccessCost: 40));
            t.RegisterGasAsync(Instruction.SELFDESTRUCT, SelfDestructFrontierGasCost.Instance);

            // Executors
            var arith = new ArithmeticBitwiseExecutor(_pe.Arithmetic, _pe.Bitwise, hasShifts: false, hasClz: false);
            var stack = new StackFlowExecutor(_pe.StackFlowExecution, hasPush0: false);
            var mem = new MemoryExecutor(_pe.StorageMemory, hasMCopy: false);
            var ctx = new ContextExecutor(_pe.CallInput, _pe.CallData, _pe.Code, _pe.BlockchainCurrentContractContext, hasChainId: false, hasBaseFee: false, hasBlobOps: false);
            var logRet = new LogReturnExecutor(_pe.ReturnRevertLogExecution, hasRevert: false, hasReturnData: false);
            var balCode = new BalanceCodeExecutor(_pe.BlockchainCurrentContractContext, _pe.Code, hasSelfBalance: false, hasExtCodeHash: false);
            var blockHash = new BlockHashExecutor();
            var storage = new StorageExecutor(_pe.StorageMemory, _pe.BlockchainCurrentContractContext, hasTransientStorage: false);
            var selfDestruct = new SelfDestructExecutor(_pe, SelfDestructRuleSets.Frontier);

            RegisterArithmeticBitwise(t, arith);
            RegisterStackFlow(t, stack);
            RegisterMemory(t, mem);
            RegisterContext(t, ctx);
            RegisterLogReturn(t, logRet);
            RegisterBalanceCode(t, balCode);
            RegisterBlockHash(t, blockHash);
            RegisterStorage(t, storage);
            t.RegisterExecAsync(Instruction.SELFDESTRUCT, selfDestruct);

            return t;
        }

        private static OpcodeHandlerTable BuildTangerineWhistle()
        {
            var t = BuildFrontier();
            t.RegisterGas(Instruction.EXP, new ExpGasCost(byteCost: 10));
            t.RegisterGas(Instruction.BALANCE, new FixedGasCost(400));
            t.RegisterGas(Instruction.EXTCODESIZE, new FixedGasCost(700));
            t.RegisterGas(Instruction.EXTCODECOPY, new ExtCodeCopyGasCost(fixedAccessCost: 700));
            t.RegisterGas(Instruction.SLOAD, new FixedGasCost(200));
            t.RegisterGasAsync(Instruction.SSTORE, new SstoreFrontierGasCost());
            t.RegisterGasAsync(Instruction.CALL, new CallGasCost(fixedAccessCost: 700, newAccountRequiresValue: false));
            t.RegisterGas(Instruction.CALLCODE, new CallCodeGasCost(fixedAccessCost: 700));
            t.RegisterGas(Instruction.DELEGATECALL, new DelegateCallGasCost(fixedAccessCost: 700));
            t.RegisterGasAsync(Instruction.SELFDESTRUCT, new SelfDestructGasCost(hasColdWarmAccess: false));
            return t;
        }

        private static OpcodeHandlerTable BuildSpuriousDragon()
        {
            var t = BuildTangerineWhistle();
            t.RegisterGas(Instruction.EXP, new ExpGasCost(byteCost: 50));
            t.RegisterGasAsync(Instruction.CALL, new CallGasCost(fixedAccessCost: 700, newAccountRequiresValue: true));
            return t;
        }

        private static OpcodeHandlerTable BuildByzantium()
        {
            var t = BuildSpuriousDragon();
            t.RegisterGas(Instruction.RETURNDATASIZE, FixedGasCost.G2);
            t.RegisterGas(Instruction.RETURNDATACOPY, CopyGasCost.Instance);
            t.RegisterGas(Instruction.STATICCALL, new StaticCallGasCost(fixedAccessCost: 700));
            t.RegisterGas(Instruction.REVERT, ReturnRevertGasCost.Instance);
            // New executor capabilities
            var logRet = new LogReturnExecutor(_pe.ReturnRevertLogExecution, hasRevert: true, hasReturnData: true);
            RegisterLogReturn(t, logRet);
            t.RegisterExec(Instruction.RETURNDATASIZE, logRet);
            t.RegisterExec(Instruction.RETURNDATACOPY, logRet);
            t.RegisterExec(Instruction.REVERT, logRet);
            return t;
        }

        private static OpcodeHandlerTable BuildConstantinople()
        {
            var t = BuildByzantium();
            t.RegisterGas(Instruction.SHL, FixedGasCost.G3);
            t.RegisterGas(Instruction.SHR, FixedGasCost.G3);
            t.RegisterGas(Instruction.SAR, FixedGasCost.G3);
            t.RegisterGas(Instruction.CREATE2, new Create2GasCost(hasInitCodeWordGas: false));
            t.RegisterGas(Instruction.EXTCODEHASH, new FixedGasCost(400));
            // New executor capabilities
            var arith = new ArithmeticBitwiseExecutor(_pe.Arithmetic, _pe.Bitwise, hasShifts: true, hasClz: false);
            RegisterArithmeticBitwise(t, arith);
            t.RegisterExec(Instruction.SHL, arith);
            t.RegisterExec(Instruction.SHR, arith);
            t.RegisterExec(Instruction.SAR, arith);
            var balCode = new BalanceCodeExecutor(_pe.BlockchainCurrentContractContext, _pe.Code, hasSelfBalance: false, hasExtCodeHash: true);
            RegisterBalanceCode(t, balCode);
            t.RegisterExecAsync(Instruction.EXTCODEHASH, balCode);
            return t;
        }

        private static OpcodeHandlerTable BuildIstanbul()
        {
            var t = BuildConstantinople();
            t.RegisterGas(Instruction.SLOAD, new FixedGasCost(800));
            t.RegisterGasAsync(Instruction.SSTORE, new SstoreFixedGasCost(sloadGas: 800));
            t.RegisterGas(Instruction.BALANCE, new FixedGasCost(700));
            t.RegisterGas(Instruction.EXTCODEHASH, new FixedGasCost(700));
            t.RegisterGas(Instruction.CHAINID, FixedGasCost.G2);
            t.RegisterGas(Instruction.SELFBALANCE, FixedGasCost.G5);
            // New executor capabilities
            var ctx = new ContextExecutor(_pe.CallInput, _pe.CallData, _pe.Code, _pe.BlockchainCurrentContractContext, hasChainId: true, hasBaseFee: false, hasBlobOps: false);
            RegisterContext(t, ctx);
            t.RegisterExec(Instruction.CHAINID, ctx);
            var balCode = new BalanceCodeExecutor(_pe.BlockchainCurrentContractContext, _pe.Code, hasSelfBalance: true, hasExtCodeHash: true);
            RegisterBalanceCode(t, balCode);
            t.RegisterExecAsync(Instruction.SELFBALANCE, balCode);
            t.RegisterExecAsync(Instruction.EXTCODEHASH, balCode);
            return t;
        }

        private static OpcodeHandlerTable BuildBerlin()
        {
            var t = BuildIstanbul();
            t.RegisterGas(Instruction.BALANCE, new AccountAccessGasCost(_eip2929Account));
            t.RegisterGas(Instruction.EXTCODESIZE, new AccountAccessGasCost(_eip2929Account));
            t.RegisterGas(Instruction.EXTCODEHASH, new AccountAccessGasCost(_eip2929Account));
            t.RegisterGas(Instruction.EXTCODECOPY, new ExtCodeCopyGasCost(_eip2929Account));
            t.RegisterGas(Instruction.SLOAD, new SloadGasCost(_eip2929Storage));
            t.RegisterGasAsync(Instruction.SSTORE, new SstoreGasCost(_eip2929Storage));
            t.RegisterGasAsync(Instruction.CALL, new CallGasCost(_eip2929Account));
            t.RegisterGas(Instruction.CALLCODE, new CallCodeGasCost(_eip2929Account));
            t.RegisterGas(Instruction.DELEGATECALL, new DelegateCallGasCost(_eip2929Account));
            t.RegisterGas(Instruction.STATICCALL, new StaticCallGasCost(_eip2929Account));
            t.RegisterGasAsync(Instruction.SELFDESTRUCT, new SelfDestructGasCost(hasColdWarmAccess: true));
            return t;
        }

        private static OpcodeHandlerTable BuildLondon()
        {
            var t = BuildBerlin();
            t.RegisterGas(Instruction.BASEFEE, FixedGasCost.G2);
            // New executor capabilities
            var ctx = new ContextExecutor(_pe.CallInput, _pe.CallData, _pe.Code, _pe.BlockchainCurrentContractContext, hasChainId: true, hasBaseFee: true, hasBlobOps: false);
            RegisterContext(t, ctx);
            t.RegisterExec(Instruction.BASEFEE, ctx);
            // London selfdestruct: refund=0
            var selfDestruct = new SelfDestructExecutor(_pe, SelfDestructRuleSets.London);
            t.RegisterExecAsync(Instruction.SELFDESTRUCT, selfDestruct);
            return t;
        }

        private static OpcodeHandlerTable BuildShanghai()
        {
            var t = BuildLondon();
            t.RegisterGas(Instruction.PUSH0, FixedGasCost.G2);
            t.RegisterGas(Instruction.CREATE, new CreateGasCost(hasInitCodeWordGas: true));
            t.RegisterGas(Instruction.CREATE2, new Create2GasCost(hasInitCodeWordGas: true));
            // New executor capabilities
            var stack = new StackFlowExecutor(_pe.StackFlowExecution, hasPush0: true);
            RegisterStackFlow(t, stack);
            t.RegisterExec(Instruction.PUSH0, stack);
            return t;
        }

        private static OpcodeHandlerTable BuildCancun()
        {
            var t = BuildShanghai();
            t.RegisterGas(Instruction.TLOAD, FixedGasCost.G100);
            t.RegisterGas(Instruction.TSTORE, FixedGasCost.G100);
            t.RegisterGas(Instruction.MCOPY, MCopyGasCost.Instance);
            t.RegisterGas(Instruction.BLOBHASH, FixedGasCost.G3);
            t.RegisterGas(Instruction.BLOBBASEFEE, FixedGasCost.G2);
            // New executor capabilities
            var mem = new MemoryExecutor(_pe.StorageMemory, hasMCopy: true);
            RegisterMemory(t, mem);
            t.RegisterExec(Instruction.MCOPY, mem);
            var ctx = new ContextExecutor(_pe.CallInput, _pe.CallData, _pe.Code, _pe.BlockchainCurrentContractContext, hasChainId: true, hasBaseFee: true, hasBlobOps: true);
            RegisterContext(t, ctx);
            t.RegisterExec(Instruction.BLOBHASH, ctx);
            t.RegisterExec(Instruction.BLOBBASEFEE, ctx);
            var storage = new StorageExecutor(_pe.StorageMemory, _pe.BlockchainCurrentContractContext, hasTransientStorage: true);
            RegisterStorage(t, storage);
            t.RegisterExecAsync(Instruction.TLOAD, storage);
            t.RegisterExecAsync(Instruction.TSTORE, storage);
            // Cancun selfdestruct: EIP-6780
            var selfDestruct = new SelfDestructExecutor(_pe, SelfDestructRuleSets.Cancun);
            t.RegisterExecAsync(Instruction.SELFDESTRUCT, selfDestruct);
            return t;
        }

        private static OpcodeHandlerTable BuildOsaka()
        {
            // Prague has no opcode-layer changes over Cancun (EIP-2935 BLOCKHASH uses
            // the same executor). Osaka layers CLZ on top.
            var t = BuildCancun();
            t.RegisterGas(Instruction.CLZ, FixedGasCost.G5);
            var arith = new ArithmeticBitwiseExecutor(_pe.Arithmetic, _pe.Bitwise, hasShifts: true, hasClz: true);
            RegisterArithmeticBitwise(t, arith);
            t.RegisterExec(Instruction.SHL, arith);
            t.RegisterExec(Instruction.SHR, arith);
            t.RegisterExec(Instruction.SAR, arith);
            t.RegisterExec(Instruction.CLZ, arith);
            return t;
        }

    }
}
