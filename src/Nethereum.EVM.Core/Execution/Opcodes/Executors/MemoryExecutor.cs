namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class MemoryExecutor : IOpcodeExecutor
    {
        private readonly EvmStorageMemoryExecution _storageMemory;
        private readonly bool _hasMCopy;

        public MemoryExecutor(EvmStorageMemoryExecution storageMemory, bool hasMCopy = true)
        {
            _storageMemory = storageMemory;
            _hasMCopy = hasMCopy;
        }

        public bool Execute(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.MLOAD:
                    _storageMemory.MLoad(program);
                    return true;
                case Instruction.MSTORE:
                    _storageMemory.MStore(program);
                    return true;
                case Instruction.MSTORE8:
                    _storageMemory.MStore8(program);
                    return true;
                case Instruction.MSIZE:
                    _storageMemory.MSize(program);
                    return true;
                case Instruction.MCOPY:
                    if (!_hasMCopy) return false;
                    _storageMemory.MCopy(program);
                    return true;
                default:
                    return false;
            }
        }
    }
}
