using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nethereum.EVM
{

    public class Program
    {
        public const int MAX_STACKSIZE = 1024;
        private readonly byte[][] stack = new byte[MAX_STACKSIZE][];
        private int stackPointer = 0;
        public List<byte> Memory { get; set; }
        public List<ProgramInstruction> Instructions { get; private set; }
        public List<ProgramTrace> Trace { get; private set; }
        public ProgramResult ProgramResult { get; private set; }


        public List<string> GetCurrentStackAsHex()
        {
            var result = new List<string>(stackPointer);
            for (int i = stackPointer - 1; i >= 0; i--)
            {
                result.Add(stack[i].ToHex());
            }
            return result;
        }

        public string GetCurrentMemoryAsHex()
        {
            return Memory.ToArray().ToHex();
        }

        

        private int currentInstructionIndex = 0;

        public Program(byte[] bytecode, ProgramContext programContext = null)
        {
            this.Instructions = ProgramInstructionsUtils.GetProgramInstructions(bytecode);
            this.Memory = new List<byte>();
            ByteCode = bytecode;
            ProgramContext = programContext;
            Trace = new List<ProgramTrace>();
            ProgramResult = new ProgramResult();

            if (programContext != null)
            {
                GasRemaining = programContext.Gas;
            }
            else
            {
                // For testing without context, provide unlimited gas
                GasRemaining = BigInteger.Parse("10000000000000");
            }

            // Handle empty bytecode - immediately mark as stopped
            if (this.Instructions.Count == 0)
            {
                StoppedImplicitly = true;
                Stop();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPush(byte[] stackWord)
        {
            ThrowWhenPushStackOverflows();
            stack[stackPointer++] = stackWord.PadTo32Bytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] StackPeek()
        {
            return stack[stackPointer - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] StackPeekAt(int index)
        {
            int actualIndex = stackPointer - 1 - index;
            if (actualIndex < 0 || index < 0)
            {
                throw new Exception("Invalid stack index");
            }
            return stack[actualIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigInteger StackPeekAtAndConvertToBigInteger(int index)
        {
            return StackPeekAt(index).ConvertToInt256();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigInteger StackPeekAtAndConvertToUBigInteger(int index)
        {
            return StackPeekAt(index).ConvertToUInt256();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackSwap(int index)
        {
            if (stackPointer < index + 1)
            {
                throw new Exception($"Stack underflow: SWAP{index} requires {index + 1} items, have {stackPointer}");
            }
            int topIndex = stackPointer - 1;
            int swapIndex = stackPointer - 1 - index;
            var swapTemp = stack[topIndex];
            stack[topIndex] = stack[swapIndex];
            stack[swapIndex] = swapTemp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackDup(int index)
        {
            if (stackPointer < index)
            {
                throw new Exception($"Stack underflow: DUP{index} requires {index} items, have {stackPointer}");
            }
            var dup = stack[stackPointer - index];
            StackPush(dup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] StackPop()
        {
            if (stackPointer == 0)
            {
                throw new Exception("Stack underflow");
            }
            return stack[--stackPointer];
        }

        private void ThrowWhenPushStackOverflows()
        {
            VerifyStackOverflow(0,1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VerifyStackOverflow(int args, int returns)
        {
            if (stackPointer - args + returns > MAX_STACKSIZE)
            {
                throw new Exception("Stack overflow, maximum size is " + MAX_STACKSIZE);
            }
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigInteger StackPopAndConvertToBigInteger()
        {
            return StackPop().ConvertToInt256();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigInteger StackPopAndConvertToUBigInteger()
        {
            return StackPop().ConvertToUInt256();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPushSigned(BigInteger value)
        {
            if (value < 0)
            {
                value = 1 + IntType.MAX_UINT256_VALUE + value;
            }
            StackPush(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPush(BigInteger value)
        {
            StackPush(IntTypeEncoder.EncodeSignedUnsigned256(value, 32));
        }

        public bool IsAddressWarm(byte[] addressBytes)
        {
            var address = ExtractAddressFromBytes(addressBytes);
            return ProgramContext.ExecutionStateService.AddressIsWarm(address);
        }

        public void MarkAddressAsWarm(byte[] addressBytes)
        {
            var address = ExtractAddressFromBytes(addressBytes);
            ProgramContext.ExecutionStateService.MarkAddressAsWarm(address);
        }

        private static string ExtractAddressFromBytes(byte[] addressBytes)
        {
            if (addressBytes.Length <= 20)
            {
                return addressBytes.ToHex();
            }
            var last20Bytes = new byte[20];
            Array.Copy(addressBytes, addressBytes.Length - 20, last20Bytes, 0, 20);
            return last20Bytes.ToHex();
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

            var highestAccessedByte = offset + length;

            if (highestAccessedByte > int.MaxValue)
                return GasConstants.OVERFLOW_GAS_COST;

            var currentBytes = Memory?.Count ?? 0;
            var currentWords = (currentBytes + 31) / 32;

            var requiredWords = (highestAccessedByte + 31) / 32;

            if (requiredWords <= currentWords)
                return 0;

            return MemoryCost(requiredWords) - MemoryCost(currentWords);
        }

        private static BigInteger MemoryCost(BigInteger words)
        {
            return (words * words / 512) + (3 * words);
        }

        public BigInteger TotalGasUsed { get; set; } = 0;
        public BigInteger GasRemaining { get; set; } = 0;
        public BigInteger RefundCounter { get; set; } = 0;

        public void UpdateGasUsed(BigInteger gasCost)
        {
            if (GasRemaining < gasCost)
            {
                throw new OutOfGasException(gasCost, GasRemaining);
            }

            TotalGasUsed += gasCost;
            GasRemaining -= gasCost;
        }

        public void AddRefund(BigInteger refund)
        {
            RefundCounter += refund;
        }

        public BigInteger GetEffectiveRefund()
        {
            var maxRefund = TotalGasUsed / GasConstants.REFUND_QUOTIENT;
            return BigInteger.Min(RefundCounter, maxRefund);
        }
    }
}