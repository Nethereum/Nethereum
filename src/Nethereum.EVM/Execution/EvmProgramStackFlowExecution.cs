namespace Nethereum.EVM.Execution
{
    public class EvmProgramStackFlowExecution
    {
        public void Pop(Program program)
        {
            program.StackPop();
            program.Step();
        }

        public void Jump(Program program)
        {
            var dest = (int)program.StackPopAndConvertToBigInteger();
            program.GoToJumpDestination(dest);
        }

        public void Jumpi(Program program)
        {
            var desti = (int)program.StackPopAndConvertToBigInteger();
            var valid = program.StackPopAndConvertToBigInteger();
            if (valid != 0)
            {
                program.GoToJumpDestination(desti);
            }
            else
            {
                program.Step();
            }

        }
        public void JumpDest(Program program)
        {
            program.Step();
        }

        public void PC(Program program)
        {
            var pc = program.GetProgramCounter();
            program.StackPush(pc);
            program.Step();
        }

        public void Push(Program program)
        {
            var data = program.GetCurrentInstruction().Arguments;
            program.StackPush(data.PadTo32Bytes());
            program.Step();
        }

        public void Dup(Program program)
        {
            int dupIndex = (int)program.GetCurrentInstruction().Value - (int)Instruction.DUP1 + 1;
            program.StackDup(dupIndex);
            program.Step();
        }

        public void Swap(Program program)
        {
            int swapIndex = (int)program.GetCurrentInstruction().Value - (int)Instruction.SWAP1 + 1;
            program.StackSwap(swapIndex);
            program.Step();
        }
    }
}