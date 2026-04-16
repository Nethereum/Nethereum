namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class StackFlowExecutor : IOpcodeExecutor
    {
        private readonly EvmProgramStackFlowExecution _stackFlow;
        private readonly bool _hasPush0;

        public StackFlowExecutor(EvmProgramStackFlowExecution stackFlow, bool hasPush0 = true)
        {
            _stackFlow = stackFlow;
            _hasPush0 = hasPush0;
        }

        public bool Execute(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.STOP:
                    program.Stop();
                    return true;
                case Instruction.POP:
                    _stackFlow.Pop(program);
                    return true;
                case Instruction.JUMP:
                    _stackFlow.Jump(program);
                    return true;
                case Instruction.JUMPI:
                    _stackFlow.Jumpi(program);
                    return true;
                case Instruction.JUMPDEST:
                    _stackFlow.JumpDest(program);
                    return true;
                case Instruction.PC:
                    _stackFlow.PC(program);
                    return true;
                case Instruction.PUSH0:
                    if (!_hasPush0) return false;
                    _stackFlow.PushZero(program);
                    return true;
                case Instruction.INVALID:
                    program.GasRemaining = 0;
                    program.ProgramResult.IsRevert = true;
                    program.Stop();
                    return true;
            }

            if (opcode >= Instruction.PUSH1 && opcode <= Instruction.PUSH32)
            {
                _stackFlow.Push(program);
                return true;
            }
            if (opcode >= Instruction.DUP1 && opcode <= Instruction.DUP16)
            {
                _stackFlow.Dup(program);
                return true;
            }
            if (opcode >= Instruction.SWAP1 && opcode <= Instruction.SWAP16)
            {
                _stackFlow.Swap(program);
                return true;
            }

            return false;
        }
    }
}
