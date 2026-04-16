using System;
using System.Runtime.CompilerServices;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmProgramStackFlowExecution
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pop(Program program)
        {
            program.StackPop();
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Jump(Program program)
        {
            var destU256 = program.StackPopU256();
            if (!destU256.FitsInInt)
            {
#if EVM_SYNC
                program.SetExecutionError(); return;
#else
                throw new Exception("Invalid jump destination");
#endif
            }
            var dest = destU256.ToInt();
            program.GoToJumpDestination(dest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Jumpi(Program program)
        {
            var destiU256 = program.StackPopU256();
            var validU256 = program.StackPopU256();
            if (!validU256.IsZero)
            {
                if (!destiU256.FitsInInt)
                {
#if EVM_SYNC
                    program.SetExecutionError(); return;
#else
                    throw new Exception("Invalid jump destination");
#endif
                }
                var desti = destiU256.ToInt();
                program.GoToJumpDestination(desti);
            }
            else
            {
                program.Step();
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void JumpDest(Program program)
        {
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PC(Program program)
        {
            var pc = program.GetProgramCounter();
            program.StackPush(pc);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushZero(Program program)
        {
            program.StackPush(0);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(Program program)
        {
            var data = program.GetCurrentInstruction().Arguments;
            program.StackPush(data.PadTo32Bytes());
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dup(Program program)
        {
            int dupIndex = (int)program.GetCurrentInstruction().Value - (int)Instruction.DUP1 + 1;
            program.StackDup(dupIndex);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Swap(Program program)
        {
            int swapIndex = (int)program.GetCurrentInstruction().Value - (int)Instruction.SWAP1 + 1;
            program.StackSwap(swapIndex);
            program.Step();
        }
    }
}