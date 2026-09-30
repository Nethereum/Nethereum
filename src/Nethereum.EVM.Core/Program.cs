using Nethereum.Util;
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nethereum.EVM
{

    public class Program
    {
        public const int MAX_STACKSIZE = 1024;
        private readonly EvmUInt256[] stack = new EvmUInt256[MAX_STACKSIZE];
        private int stackPointer = 0;
        public List<byte> Memory { get; set; }
        public List<ProgramInstruction> Instructions { get; private set; }
        private Dictionary<int, int> jumpDestinationIndexesByStep;
        public List<ProgramTrace> Trace { get; private set; }
        public ProgramResult ProgramResult { get; private set; }


        public List<string> GetCurrentStackAsHex()
        {
            var result = new List<string>(stackPointer);
            for (int i = stackPointer - 1; i >= 0; i--)
            {
                result.Add(stack[i].ToBigEndian().ToHex());
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
            var analysis = GetOrBuildAnalysis(bytecode, eip8024Enabled: false);
            this.Instructions = analysis.Instructions;
            this.jumpDestinationIndexesByStep = analysis.JumpDestinationIndexesByStep;
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
                GasRemaining = 10000000000000L;
            }

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

        public void ApplyEip8024Decoding()
        {
            var analysis = GetOrBuildAnalysis(ByteCode, eip8024Enabled: true);
            Instructions = analysis.Instructions;
            jumpDestinationIndexesByStep = analysis.JumpDestinationIndexesByStep;
        }

        private static readonly ConditionalWeakTable<byte[], ProgramCodeAnalysis> AnalysisCache =
            new ConditionalWeakTable<byte[], ProgramCodeAnalysis>();
        private static readonly ConditionalWeakTable<byte[], ProgramCodeAnalysis> AnalysisCacheEip8024 =
            new ConditionalWeakTable<byte[], ProgramCodeAnalysis>();

        private static ProgramCodeAnalysis GetOrBuildAnalysis(byte[] byteCode, bool eip8024Enabled)
        {
            var cache = eip8024Enabled ? AnalysisCacheEip8024 : AnalysisCache;
            if (cache.TryGetValue(byteCode, out var cached)) return cached;
            var instructions = ProgramInstructionsUtils.GetProgramInstructions(byteCode, eip8024Enabled);
            var analysis = new ProgramCodeAnalysis(instructions, BuildJumpDestinationMap(instructions));
            return cache.GetValue(byteCode, _ => analysis);
        }

        private static Dictionary<int, int> BuildJumpDestinationMap(List<ProgramInstruction> instructions)
        {
            var map = new Dictionary<int, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].Instruction == Instruction.JUMPDEST)
                {
                    map[instructions[i].Step] = i;
                }
            }
            return map;
        }

        private sealed class ProgramCodeAnalysis
        {
            public ProgramCodeAnalysis(List<ProgramInstruction> instructions, Dictionary<int, int> jumpDestinationIndexesByStep)
            {
                Instructions = instructions;
                JumpDestinationIndexesByStep = jumpDestinationIndexesByStep;
            }

            public List<ProgramInstruction> Instructions { get; }
            public Dictionary<int, int> JumpDestinationIndexesByStep { get; }
        }

        public void GoToJumpDestination(int step)
        {
            if (jumpDestinationIndexesByStep.TryGetValue(step, out var index))
            {
                SetInstrunctionIndex(index);
                return;
            }
#if EVM_SYNC
            SetExecutionError();
#else
            throw new Exception("Invalid jump destination");
#endif

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

        public bool IsExceptionalHalt { get; private set; } = false;

        public void MarkExceptionalHalt()
        {
            IsExceptionalHalt = true;
        }

#if EVM_SYNC
        public bool HasExecutionError { get; private set; } = false;

        public void SetExecutionError()
        {
            HasExecutionError = true;
            IsExceptionalHalt = true;
            GasRemaining = 0;
            ProgramResult.IsRevert = true;
            Stop();
        }
#endif
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
            stack[stackPointer++] = EvmUInt256.FromBigEndian(stackWord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] StackPeek()
        {
            return stack[stackPointer - 1].ToBigEndian();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] StackPeekAt(int index)
        {
            int actualIndex = stackPointer - 1 - index;
            if (actualIndex < 0 || index < 0)
            {
#if EVM_SYNC
                SetExecutionError();
                return new byte[32];
#else
                throw new Exception("Invalid stack index");
#endif
            }
            return stack[actualIndex].ToBigEndian();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256 StackPeekAtU256(int index)
        {
            int actualIndex = stackPointer - 1 - index;
            if (actualIndex < 0 || index < 0)
            {
#if EVM_SYNC
                SetExecutionError();
                return EvmUInt256.Zero;
#else
                throw new Exception("Invalid stack index");
#endif
            }
            return stack[actualIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256 StackPeekAtI256(int index)
        {
            return StackPeekAtU256(index);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackSwap(int index)
        {
            if (stackPointer < index + 1)
            {
#if EVM_SYNC
                SetExecutionError();
                return;
#else
                throw new Exception($"Stack underflow: SWAP{index} requires {index + 1} items, have {stackPointer}");
#endif
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
#if EVM_SYNC
                SetExecutionError();
                return;
#else
                throw new Exception($"Stack underflow: DUP{index} requires {index} items, have {stackPointer}");
#endif
            }
            var src = stack[stackPointer - index];
            ThrowWhenPushStackOverflows();
            stack[stackPointer++] = src;
        }

        /// <summary>
        /// EIP-8024 EXCHANGE: "Swap <c>stack[top - n]</c> and
        /// <c>stack[top - m]</c>", where n and m are the two parameters as
        /// <see cref="Eip8024ImmediateCodec.TryDecodePair"/> decoded them.
        /// Unlike <see cref="StackSwap"/>, neither side has to be the top:
        /// EXCHANGE's decoded depths are always &gt;= 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackExchange(int depthFromTopA, int depthFromTopB)
        {
            int maxDepth = depthFromTopA > depthFromTopB ? depthFromTopA : depthFromTopB;
            if (stackPointer < maxDepth + 1)
            {
#if EVM_SYNC
                SetExecutionError();
                return;
#else
                throw new Exception($"Stack underflow: EXCHANGE requires {maxDepth + 1} items, have {stackPointer}");
#endif
            }
            int indexA = stackPointer - 1 - depthFromTopA;
            int indexB = stackPointer - 1 - depthFromTopB;
            var temp = stack[indexA];
            stack[indexA] = stack[indexB];
            stack[indexB] = temp;
        }

        public void RaiseInvalidEip8024Immediate(string opcodeName)
        {
#if EVM_SYNC
            SetExecutionError();
#else
            throw new Exception($"{opcodeName}: immediate byte outside EIP-8024 valid range");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] StackPop()
        {
            if (stackPointer == 0)
            {
#if EVM_SYNC
                SetExecutionError();
                return new byte[32];
#else
                throw new Exception("Stack underflow");
#endif
            }
            return stack[--stackPointer].ToBigEndian();
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
#if EVM_SYNC
                SetExecutionError();
                return;
#else
                throw new Exception("Stack overflow, maximum size is " + MAX_STACKSIZE);
#endif
            }
        }

        public void ExpandMemory(int newSize)
        {
            if (newSize > Memory.Count)
            {
                var gap = newSize - Memory.Count;
                var extraSize = ((gap + 31) / 32) * 32;
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

            if (data == null) data = new byte[0];


            int newSize = index + totalSize;


            if (newSize > Memory.Count)
            {
                if (extend)
                {
                    var gap = newSize - Memory.Count;
                    var extraSize = ((gap + 31) / 32) * 32;
                    Memory.AddRange(new byte[extraSize]);
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
        public EvmUInt256 StackPopU256()
        {
            if (stackPointer == 0)
            {
#if EVM_SYNC
                SetExecutionError();
                return EvmUInt256.Zero;
#else
                throw new Exception("Stack underflow");
#endif
            }
            return stack[--stackPointer];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPush(EvmUInt256 value)
        {
            ThrowWhenPushStackOverflows();
            stack[stackPointer++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPush(long value)
        {
            StackPush(new EvmUInt256(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPush(int value)
        {
            StackPush(new EvmUInt256((long)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPushSigned(EvmUInt256 value)
        {
            ThrowWhenPushStackOverflows();
            stack[stackPointer++] = value;
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
            string hex;
            if (addressBytes.Length <= 20)
            {
                hex = addressBytes.ToHex();
            }
            else
            {
                var last20Bytes = new byte[20];
                Array.Copy(addressBytes, addressBytes.Length - 20, last20Bytes, 0, 20);
                hex = last20Bytes.ToHex();
            }
            return AddressUtil.Current.ConvertToValid20ByteAddress(hex);
        }

        public bool IsStorageSlotWarm(EvmUInt256 key)
        {
            var state = ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(ProgramContext.AddressContract);
            return state.IsStorageKeyWarm(key);
        }

        public void MarkStorageSlotAsWarm(EvmUInt256 key)
        {
            var state = ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(ProgramContext.AddressContract);
            state.MarkStorageKeyAsWarm(key);
        }

        public long CalculateMemoryExpansionGas(EvmUInt256 offset, EvmUInt256 length)
        {
            if (length.IsZero) return 0;

            var highestAccessedByte = offset + length;

            if (highestAccessedByte < offset || !highestAccessedByte.FitsInInt)
                return GasConstants.OVERFLOW_GAS_COST;

            return CalculateMemoryExpansionGas(offset.ToLongSafe(), length.ToLongSafe());
        }

        public long CalculateMemoryExpansionGas(long offset, long length)
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

        private static long MemoryCost(long words)
        {
            return (words * words / 512) + (3 * words);
        }

        public long TotalGasUsed { get; set; } = 0;
        public long GasRemaining { get; set; } = 0;
        public long RefundCounter { get; set; } = 0;

        public long StateGasLeft { get; set; } = 0;
        public long StateGasBaseline { get; set; } = 0;
        public long StateGasSpilled { get; set; } = 0;

        public bool CallNewAccountStateGasCharged { get; set; } = false;

        public void RequireGas(long gasCost)
        {
            if (GasRemaining >= gasCost) return;
#if EVM_SYNC
            SetExecutionError();
#else
            throw new OutOfGasException(gasCost, GasRemaining);
#endif
        }

        public void UpdateGasUsed(long gasCost)
        {
            RequireGas(gasCost);
#if EVM_SYNC
            if (HasExecutionError) return;
#endif
            TotalGasUsed += gasCost;
            GasRemaining -= gasCost;
        }

        public void AddRefund(long refund)
        {
            RefundCounter += refund;
        }
    }
}