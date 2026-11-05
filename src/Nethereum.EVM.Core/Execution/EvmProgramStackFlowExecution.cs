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
            program.StackPopU256();
            program.Advance(1);
        }

        public void Jump(Program program)
        {
            var destU256 = program.StackPopU256();
            if (!destU256.FitsInInt)
            {
                program.Halt(EvmHaltReason.InvalidJumpDestination);
                return;
            }
            var dest = destU256.ToInt();
            program.GoToJumpDestination(dest);
        }

        public void Jumpi(Program program)
        {
            var destiU256 = program.StackPopU256();
            var validU256 = program.StackPopU256();
            if (!validU256.IsZero)
            {
                if (!destiU256.FitsInInt)
                {
                    program.Halt(EvmHaltReason.InvalidJumpDestination);
                    return;
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
            program.Advance(1);
        }

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
            program.Advance(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(Program program)
        {
            program.StackPush(program.CurrentPushValue);
            program.Advance(1 + BytecodeDecoder.PushDataLength(program.CurrentOpcodeByte));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dup(Program program)
        {
            int dupIndex = program.CurrentOpcodeByte - (int)Instruction.DUP1 + 1;
            program.StackDup(dupIndex);
            program.Advance(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Swap(Program program)
        {
            int swapIndex = program.CurrentOpcodeByte - (int)Instruction.SWAP1 + 1;
            program.StackSwap(swapIndex);
            program.Advance(1);
        }


        public void Dupn(Program program)
        {
            var immediate = program.CurrentImmediateByte;
            if (!Eip8024ImmediateCodec.TryDecodeSingle(immediate, out int n))
            {
                program.RaiseInvalidEip8024Immediate("DUPN");
                return;
            }
            program.StackDup(n);
            program.Step();
        }

        public void Swapn(Program program)
        {
            var immediate = program.CurrentImmediateByte;
            if (!Eip8024ImmediateCodec.TryDecodeSingle(immediate, out int n))
            {
                program.RaiseInvalidEip8024Immediate("SWAPN");
                return;
            }
            program.StackSwap(n);
            program.Step();
        }

        public void Exchange(Program program)
        {
            var immediate = program.CurrentImmediateByte;
            if (!Eip8024ImmediateCodec.TryDecodePair(immediate, out int n, out int m))
            {
                program.RaiseInvalidEip8024Immediate("EXCHANGE");
                return;
            }
            program.StackExchange(n, m);
            program.Step();
        }
    }
}