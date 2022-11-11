using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Shh.KeyPair;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.EVM
{

    public class Program
    {
        private List<byte[]> stack { get; set; }
        public List<byte> Memory { get; set; }
        public List<ProgramInstruction> Instructions { get; private set; }

        public ProgramResult ProgramResult { get; private set; }


        public List<string> GetCurrentStackAsHex()
        {
            return stack.Select(x => x.ToHex()).ToList();
        }

        public string GetCurrentMemoryAsHex()
        {
            return Memory.ToArray().ToHex();
        }

        

        private int currentInstructionIndex = 0;

        public Program(byte[] bytecode, ProgramContext programContext = null)
        {
            this.Instructions = ProgramInstructionsUtils.GetProgramInstructions(bytecode);
            this.stack = new List<byte[]>();
            this.Memory = new List<byte>();
            ByteCode = bytecode;
            ProgramContext = programContext;
            ProgramResult = new ProgramResult();
        }

        public ProgramInstruction GetCurrentInstruction()
        {
            return Instructions[currentInstructionIndex];
        }

        public void GoToJumpDestination(int step)
        {
            var jump = this.Instructions.Where(x => x.Step == step).FirstOrDefault();
            if (jump != null && jump.Instruction == Instruction.JUMPDEST) 
            {
                 SetInstrunctionIndex(this.Instructions.IndexOf(jump));
            }
            else
            {
                throw new Exception("Invalid jump destination");
            }

        }

        public int GetProgramCounter()
        {
            return GetCurrentInstruction().Step;
        }

        public virtual void SetInstrunctionIndex(int index)
        {
            currentInstructionIndex = index;

            if (currentInstructionIndex >= Instructions.Count)
            {
                Stop();
            }
        }

        public bool Stopped { get; private set; } = false;
        public byte[] ByteCode { get; }
        public ProgramContext ProgramContext { get; }

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

        public void StackPush(byte[] stackWord)
        {
            ThrowWhenPushStackOverflows(); 
            stack.Insert(0, stackWord);
        }

        public byte[] StackPeek()
        {
            return stack[0];
        }

        public void StackSwap(int index)
        {
            var swapTemp = stack[0];
            stack[0] = stack[index];
            stack[index] = swapTemp;
        }

        public void StackDup(int index)
        {
            var dup = stack[index - 1];
            StackPush(dup);
        }

        public byte[] StackPop()
        {
            var popItem = stack[0];
            stack.RemoveAt(0);
            return popItem;
        }

        private void ThrowWhenPushStackOverflows()
        {
            VerifyStackOverflow(0,1);
        }

        public void VerifyStackOverflow(int args, int returns)
        {
            
            if (stack.Count - args + returns > MAX_STACKSIZE)
            {
                throw new Exception("Stack overflow, maximum size is " + MAX_STACKSIZE);
            }
        }

        public const int MAX_STACKSIZE = 1024;

    }
}