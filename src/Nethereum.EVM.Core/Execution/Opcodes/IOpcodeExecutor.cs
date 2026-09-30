namespace Nethereum.EVM.Execution.Opcodes
{
    public interface IOpcodeExecutor
    {
        bool Execute(Instruction opcode, Program program);
    }
}
