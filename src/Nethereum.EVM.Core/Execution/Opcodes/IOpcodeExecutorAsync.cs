#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes
{
    public interface IOpcodeExecutorAsync
    {
#if EVM_SYNC
        bool Execute(Instruction opcode, Program program);
#else
        Task<bool> ExecuteAsync(Instruction opcode, Program program);
#endif
    }
}
