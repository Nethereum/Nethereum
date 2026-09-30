#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class StorageExecutor : IOpcodeExecutorAsync
    {
        private readonly EvmStorageMemoryExecution _storageMemory;
        private readonly EvmBlockchainCurrentContractContextExecution _context;
        private readonly bool _hasTransientStorage;

        public StorageExecutor(
            EvmStorageMemoryExecution storageMemory,
            EvmBlockchainCurrentContractContextExecution context,
            bool hasTransientStorage = true)
        {
            _storageMemory = storageMemory;
            _context = context;
            _hasTransientStorage = hasTransientStorage;
        }

#if EVM_SYNC
        public bool Execute(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.SLOAD:
                    _storageMemory.SLoad(program);
                    return true;
                case Instruction.SSTORE:
                    _storageMemory.SStore(program);
                    return true;
                case Instruction.TLOAD:
                    if (!_hasTransientStorage) return false;
                    _context.TLoad(program);
                    return true;
                case Instruction.TSTORE:
                    if (!_hasTransientStorage) return false;
                    _context.TStore(program);
                    return true;
                default:
                    return false;
            }
        }
#else
        public async Task<bool> ExecuteAsync(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.SLOAD:
                    await _storageMemory.SLoad(program);
                    return true;
                case Instruction.SSTORE:
                    await _storageMemory.SStore(program);
                    return true;
                case Instruction.TLOAD:
                    if (!_hasTransientStorage) return false;
                    _context.TLoad(program);
                    return true;
                case Instruction.TSTORE:
                    if (!_hasTransientStorage) return false;
                    _context.TStore(program);
                    return true;
                default:
                    return false;
            }
        }
#endif
    }
}
