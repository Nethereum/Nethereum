using System;
using System.Collections.Generic;
using Nethereum.EVM.Gas.Opcodes.Costs;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes
{
    public sealed class OpcodeHandlerTable
    {
        private readonly IOpcodeGasCost[] _syncGas = new IOpcodeGasCost[256];
        private readonly IOpcodeGasCostAsync[] _asyncGas = new IOpcodeGasCostAsync[256];
        private readonly IOpcodeExecutor[] _syncExec = new IOpcodeExecutor[256];
        private readonly IOpcodeExecutorAsync[] _asyncExec = new IOpcodeExecutorAsync[256];
        private bool _frozen;

        public bool IsFrozen => _frozen;

        public OpcodeHandlerTable Freeze()
        {
            _frozen = true;
            return this;
        }

        private void ThrowIfFrozen()
        {
            if (_frozen)
                throw new InvalidOperationException(
                    "OpcodeHandlerTable is frozen. Per-fork tables are immutable after HardforkConfig.Build. " +
                    "Clone the table into a new instance before registering additional handlers.");
        }

        // --- Registration: gas + executor together ---

        public OpcodeHandlerTable Register(Instruction opcode, IOpcodeGasCost gas, IOpcodeExecutor exec)
        {
            ThrowIfFrozen();
            _syncGas[(int)opcode] = gas;
            _syncExec[(int)opcode] = exec;
            return this;
        }

        public OpcodeHandlerTable RegisterAsync(Instruction opcode, IOpcodeGasCostAsync gas, IOpcodeExecutorAsync exec)
        {
            ThrowIfFrozen();
            _asyncGas[(int)opcode] = gas;
            _asyncExec[(int)opcode] = exec;
            return this;
        }

        // --- Registration: gas only (CALL/CREATE — execution in StepWithCallStack) ---

        public OpcodeHandlerTable RegisterGas(Instruction opcode, IOpcodeGasCost gas)
        {
            ThrowIfFrozen();
            _syncGas[(int)opcode] = gas;
            return this;
        }

        public OpcodeHandlerTable RegisterGasAsync(Instruction opcode, IOpcodeGasCostAsync gas)
        {
            ThrowIfFrozen();
            _asyncGas[(int)opcode] = gas;
            return this;
        }

        // --- Registration: executor only (override execution without changing gas) ---

        public OpcodeHandlerTable RegisterExec(Instruction opcode, IOpcodeExecutor exec)
        {
            ThrowIfFrozen();
            _syncExec[(int)opcode] = exec;
            return this;
        }

        public OpcodeHandlerTable RegisterExecAsync(Instruction opcode, IOpcodeExecutorAsync exec)
        {
            ThrowIfFrozen();
            _asyncExec[(int)opcode] = exec;
            return this;
        }

        // --- Gas dispatch ---

#if EVM_SYNC
        public long GetGasCost(Instruction opcode, Program program)
        {
            var syncCost = _syncGas[(int)opcode];
            if (syncCost != null) return syncCost.GetGasCost(program);
            var asyncCost = _asyncGas[(int)opcode];
            if (asyncCost != null) return asyncCost.GetGasCost(program);
            return 0;
        }
#else
        public async Task<long> GetGasCostAsync(Instruction opcode, Program program)
        {
            var syncCost = _syncGas[(int)opcode];
            if (syncCost != null) return syncCost.GetGasCost(program);
            var asyncCost = _asyncGas[(int)opcode];
            if (asyncCost != null) return await asyncCost.GetGasCostAsync(program);
            return 0;
        }
#endif

        // --- Execution dispatch ---

#if EVM_SYNC
        public bool Execute(Instruction opcode, Program program)
        {
            var syncExec = _syncExec[(int)opcode];
            if (syncExec != null) return syncExec.Execute(opcode, program);
            var asyncExec = _asyncExec[(int)opcode];
            if (asyncExec != null) return asyncExec.Execute(opcode, program);
            return false;
        }
#else
        public async Task<bool> ExecuteAsync(Instruction opcode, Program program)
        {
            var syncExec = _syncExec[(int)opcode];
            if (syncExec != null) return syncExec.Execute(opcode, program);
            var asyncExec = _asyncExec[(int)opcode];
            if (asyncExec != null) return await asyncExec.ExecuteAsync(opcode, program);
            return false;
        }
#endif

        // --- Inspection ---

        public bool IsRegistered(Instruction opcode)
        {
            var idx = (int)opcode;
            return _syncGas[idx] != null || _asyncGas[idx] != null
                || _syncExec[idx] != null || _asyncExec[idx] != null;
        }

        public bool HasGasCost(Instruction opcode)
        {
            var idx = (int)opcode;
            return _syncGas[idx] != null || _asyncGas[idx] != null;
        }

        public bool HasExecutor(Instruction opcode)
        {
            var idx = (int)opcode;
            return _syncExec[idx] != null || _asyncExec[idx] != null;
        }

        public IEnumerable<Instruction> GetRegisteredOpcodes()
        {
            for (int i = 0; i < 256; i++)
            {
                if (_syncGas[i] != null || _asyncGas[i] != null
                    || _syncExec[i] != null || _asyncExec[i] != null)
                {
                    yield return (Instruction)i;
                }
            }
        }

        public string Describe(Instruction opcode)
        {
            var idx = (int)opcode;
            var gasName = _syncGas[idx]?.GetType().Name ?? _asyncGas[idx]?.GetType().Name ?? "none";
            var execName = _syncExec[idx]?.GetType().Name ?? _asyncExec[idx]?.GetType().Name ?? "none";
            return $"{opcode}: gas={gasName}, exec={execName}";
        }
    }
}
