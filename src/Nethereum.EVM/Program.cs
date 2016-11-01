using System;
using System.Collections.Generic;

namespace Nethereum.EVM
{
    public class Program
    {
        public Stack<byte[]> Stack { get; set; }
        public byte[] Instructions { get; private set; }

        private int currentInstructionIndex = 0;

        public Program(byte[] instructions)
        {
            this.Instructions = instructions;
            this.Stack = new Stack<byte[]>();
        }

        public byte GetCurrentInstruction()
        {
            return Instructions?[currentInstructionIndex] ?? 0;
        }

        public virtual void SetInstrunctionIndex(int index)
        {
            currentInstructionIndex = index;

            if (currentInstructionIndex >= Instructions.Length)
            {
                Stop();
            }
        }

        public bool Stopped { get; private set; } = false;

        public void Stop()
        {
            Stopped = true;
        }

        public void Step()
        {
            Step(1);
        }

        public void Step(int steps)
        {
            SetInstrunctionIndex(currentInstructionIndex + steps);
        }

        public byte[] Sweep(int number)
        {
            var lastInstrunction = currentInstructionIndex + number;
            if (lastInstrunction > Instructions.Length)
            {
                Stop();
            }

            var data = new byte[number];
            Array.Copy(Instructions, currentInstructionIndex, data, 0, number);
            Step(number);

            return data;
        }

        public void StackPush(byte[] stackWord)
        {
            ThrowWhenPushStackOverflows(); 
            Stack.Push(stackWord);
        }

        private void ThrowWhenPushStackOverflows()
        {
            VerifyStackOverflow(0,1);
        }

        public void VerifyStackOverflow(int args, int returns)
        {
            if (Stack.Count - args + returns > MAX_STACKSIZE)
            {
                throw new Exception("Stack overflow, maximum size is " + MAX_STACKSIZE);
            }
        }

        public const int MAX_STACKSIZE = 1024;

        public byte[] StackPop()
        {
            return Stack.Pop();
        }



        /*
        public virtual void Precompile()
        {
            for (int i = 0; i < Ops.Length; ++i)
            {

                OpCode op = OpCode.code(Ops[i]);
                if (op == null)
                {
                    continue;
                }

                if (op.Equals(OpCode.JUMPDEST))
                {
                    Jumpdest.Add(i);
                }

                if (op.asInt() >= OpCode.PUSH1.asInt() && op.asInt() <= OpCode.PUSH32.asInt())
                {
                    i += op.asInt() - OpCode.PUSH1.asInt() + 1;
                }
            }
           
        } */
    }
}