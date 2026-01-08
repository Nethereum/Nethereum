using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;
using Nethereum.EVM.Exceptions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.EVM
{

    public class Program
    {
        private List<byte[]> stack { get; set; }
        public List<byte> Memory { get; set; }
        public List<ProgramInstruction> Instructions { get; private set; }
        public List<ProgramTrace> Trace { get; private set; }
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
            Trace = new List<ProgramTrace>();
            ProgramResult = new ProgramResult();

            if (programContext != null)
            {
                GasRemaining = programContext.Gas;
            }
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
                StoppedImplicitly = true;
                Stop();
            }
        }

        public bool Stopped { get; private set; } = false;
        public bool StoppedImplicitly { get; private set; } = false;
        public byte[] ByteCode { get; }
        public ProgramContext ProgramContext { get; }

        public int GetCurrentInstructionIndex()
        {
            return currentInstructionIndex;
        }

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
            stack.Insert(0, stackWord.PadTo32Bytes());
        }

        public byte[] StackPeek()
        {
            return stack[0];
        }

        public byte[] StackPeekAt(int index)
        {
            if (index < 0 || index >= stack.Count)
            {
                throw new Exception("Invalid stack index");
            }
            return stack[index];
        }

        public BigInteger StackPeekAtAndConvertToBigInteger(int index)
        {
            return StackPeekAt(index).ConvertToInt256();
        }

        public BigInteger StackPeekAtAndConvertToUBigInteger(int index)
        {
            return StackPeekAt(index).ConvertToUInt256();
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

        public void ExpandMemory(int newSize)
        {
            if (newSize > Memory.Count)
            {
                var extraSize = (int)Math.Ceiling((double)(newSize - Memory.Count) / 32) * 32;
                Memory.AddRange(new byte[extraSize]);
            }
        }

        public void WriteToMemory(int index, byte[] data)
        {
            WriteToMemory(index, data.Length, data);
        }


        public void WriteToMemory(int index, int totalSize, byte[] data, bool extend = true)
        {
            if (totalSize == 0) return;
            //totalSize might be bigger than data length so memory will be extended to match

            if (data == null) data = new byte[0];


            int newSize = index + totalSize;


            if (newSize > Memory.Count)
            {
                if (extend)
                {
                    var extraSize = Math.Ceiling((double)(newSize - Memory.Count) / 32) * 32;
                    Memory.AddRange(new byte[(int)extraSize]);
                }
            }

            for (int i = 0; i < totalSize; i++)
            {
                if (i < data.Length)
                {
                    Memory[index + i] = data[i];
                }
                else
                {
                    Memory[index + i] = 0;
                }

            }
        }

        public BigInteger StackPopAndConvertToBigInteger()
        {
            return StackPop().ConvertToInt256();
        }

        public BigInteger StackPopAndConvertToUBigInteger()
        {
            return StackPop().ConvertToUInt256();
        }

        public void StackPushSigned(BigInteger value)
        {
            if (value < 0)
            {
                value = 1 + IntType.MAX_UINT256_VALUE + value;
            }
            StackPush(value);
        }


        public void StackPush(BigInteger value)
        {
            StackPush(IntTypeEncoder.EncodeSignedUnsigned256(value, 32));
        }

        public bool IsAddressWarm(byte[] addressBytes)
        {
            return ProgramContext.ExecutionStateService.AddressIsWarm(addressBytes.ToHex());
        }

        public void MarkAddressAsWarm(byte[] addressBytes)
        {
            ProgramContext.ExecutionStateService.MarkAddressAsWarm(addressBytes.ToHex());
        }

        public bool IsStorageSlotWarm(BigInteger key)
        {
            var state = ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(ProgramContext.AddressContract);
            return state.IsStorageKeyWarm(key);
        }

        public void MarkStorageSlotAsWarm(BigInteger key)
        {
            var state = ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(ProgramContext.AddressContract);
            state.MarkStorageKeyAsWarm(key);
        }

        public BigInteger CalculateMemoryExpansionGas(BigInteger offset, BigInteger length)
        {
            if (length == 0) return 0;

            var currentBytes = Memory?.Count ?? 0;
            var currentWords = (currentBytes + 31) / 32;

            var highestAccessedByte = (int)(offset + length);
            var requiredWords = (highestAccessedByte + 31) / 32;

            if (requiredWords <= currentWords)
                return 0;

            return MemoryCost(requiredWords) - MemoryCost(currentWords);
        }

        private static BigInteger MemoryCost(int words)
        {
            return (words * words / 512) + (3 * words);
        }

        public BigInteger TotalGasUsed { get; set; } = 0;
        public BigInteger GasRemaining { get; set; } = 0;

        public void UpdateGasUsed(BigInteger gasCost)
        {
            if (GasRemaining < gasCost)
            {
                throw new OutOfGasException(gasCost, GasRemaining);
            }

            TotalGasUsed += gasCost;
            GasRemaining -= gasCost;
        }
    }
}