using Nethereum.Documentation;
using Nethereum.Util;
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nethereum.EVM
{

    public class Program
    {
        public const int MAX_STACKSIZE = 1024;
        private const int InitialStackCapacity = 16;
        private static EvmUInt256[] spareFullStack;
        private EvmUInt256[] stack = new EvmUInt256[InitialStackCapacity];
        private int stackPointer = 0;
        public List<byte> Memory { get; set; }
        private List<ProgramInstruction> instructions;
        private List<ProgramInstruction> analysedInstructions;
        private int pc;
        private bool eip8024Enabled;
        private byte[] pushDataBitmap;
#if !EVM_ZISK
        private byte[] codeHash;
        private bool jumpAnalysisIsPrivate;
#endif
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

        

        public Program(byte[] bytecode, ProgramContext programContext = null)
            : this(bytecode, null, programContext)
        {
        }

        [NethereumDocExample(DocSection.EvmSimulator, "program-code-hash", "Build a Program for known code, sharing its analysis by code hash")]
        public Program(byte[] bytecode, byte[] codeHash, ProgramContext programContext = null)
        {
            ByteCode = bytecode;
#if !EVM_ZISK
            ProgramCodeAnalysisCache.RequireValidCodeHash(codeHash);
            this.codeHash = codeHash;
#endif
            this.Memory = new List<byte>();
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

            if (bytecode.Length == 0)
            {
                StoppedImplicitly = true;
                Stop();
            }
        }

        [NethereumDocExample(DocSection.EvmSimulator, "program-code-hash", "Build a Program for init code without publishing its analysis")]
        public static Program ForInitCode(byte[] initCode, ProgramContext programContext = null)
        {
            var program = new Program(initCode, programContext);
#if !EVM_ZISK
            program.jumpAnalysisIsPrivate = true;
#endif
            return program;
        }

        public List<ProgramInstruction> Instructions
        {
            get { return instructions ?? (instructions = OwnedCopyOf(AnalysedInstructions)); }
        }

        private List<ProgramInstruction> AnalysedInstructions
        {
            get { return analysedInstructions ?? (analysedInstructions = AnalyseByteCode()); }
        }

        private static List<ProgramInstruction> OwnedCopyOf(List<ProgramInstruction> analysed)
        {
#if EVM_ZISK
            return analysed;
#else
            var copy = new List<ProgramInstruction>(analysed.Count);
            foreach (var instruction in analysed)
            {
                copy.Add(new ProgramInstruction
                {
                    Step = instruction.Step,
                    Instruction = instruction.Instruction,
                    Value = instruction.Value,
                    Arguments = (byte[])instruction.Arguments?.Clone(),
                    SourceMap = instruction.SourceMap
                });
            }
            return copy;
#endif
        }

        public ProgramInstruction GetCurrentInstruction()
        {
            return Instructions[GetCurrentInstructionIndex()];
        }

        [NethereumDocExample(DocSection.EvmSimulator, "program-code-hash", "Read the current opcode and immediate from the code")]
        public byte CurrentOpcodeByte
        {
            get { return pc < ByteCode.Length ? ByteCode[pc] : (byte)Instruction.STOP; }
        }

        [NethereumDocExample(DocSection.EvmSimulator, "program-code-hash", "Read the current opcode and immediate from the code")]
        public Instruction CurrentOpcode
        {
            get { return (Instruction)CurrentOpcodeByte; }
        }

        [NethereumDocExample(DocSection.EvmSimulator, "program-code-hash", "Read the current opcode and immediate from the code")]
        public byte[] CurrentImmediate
        {
            get { return BytecodeDecoder.ImmediateBytes(ByteCode, pc, eip8024Enabled); }
        }

        [NethereumDocExample(DocSection.EvmSimulator, "program-code-hash", "Read the current opcode and immediate from the code")]
        public EvmUInt256 CurrentPushValue
        {
            get { return EvmUInt256.FromBigEndian(ByteCode, pc + 1, BytecodeDecoder.PushDataLength(CurrentOpcodeByte)); }
        }

        [NethereumDocExample(DocSection.EvmSimulator, "program-code-hash", "Read the current opcode and immediate from the code")]
        public byte CurrentImmediateByte
        {
            get { return BytecodeDecoder.ImmediateByteAt(ByteCode, pc); }
        }

        public void ApplyEip8024Decoding()
        {
            eip8024Enabled = true;
            instructions = null;
            analysedInstructions = null;
        }

        private List<ProgramInstruction> AnalyseByteCode()
        {
#if EVM_ZISK
            return ProgramInstructionsUtils.GetProgramInstructions(ByteCode, eip8024Enabled);
#else
            codeHash = codeHash ?? ProgramCodeAnalysisCache.HashOf(ByteCode);
            return ProgramCodeAnalysisCache.GetOrBuild(ByteCode, codeHash, eip8024Enabled).Instructions;
#endif
        }

        private byte[] PushDataBitmap
        {
            get { return pushDataBitmap ?? (pushDataBitmap = ResolvePushDataBitmap()); }
        }

        private byte[] ResolvePushDataBitmap()
        {
#if EVM_ZISK
            return BytecodeDecoder.BuildPushDataBitmap(ByteCode);
#else
            if (jumpAnalysisIsPrivate) return BytecodeDecoder.BuildPushDataBitmap(ByteCode);
            codeHash = codeHash ?? ProgramCodeAnalysisCache.HashOf(ByteCode);
            return ProgramCodeAnalysisCache.GetOrBuildPushDataBitmap(ByteCode, codeHash);
#endif
        }

        public void GoToJumpDestination(int step)
        {
            if (!BytecodeDecoder.IsJumpDestination(ByteCode, PushDataBitmap, step))
            {
                Halt(EvmHaltReason.InvalidJumpDestination);
                return;
            }
            MoveTo(step);
        }

        public int GetProgramCounter()
        {
            return pc;
        }

        public virtual void SetInstrunctionIndex(int index)
        {
            var analysed = AnalysedInstructions;
            MoveTo(index < analysed.Count ? analysed[index].Step : ByteCode.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveTo(int newPc)
        {
            pc = newPc;
            if (pc >= ByteCode.Length)
            {
                StoppedImplicitly = !IsExceptionalHalt;
                Stop();
            }
        }

        private int IndexOfInstructionAt(int step)
        {
            var list = AnalysedInstructions;
            var low = 0;
            var high = list.Count - 1;
            while (low <= high)
            {
                var middle = (low + high) >> 1;
                var middleStep = list[middle].Step;
                if (middleStep == step) return middle;
                if (middleStep < step) low = middle + 1; else high = middle - 1;
            }
            throw new InvalidOperationException("The program counter is not at the start of an instruction.");
        }

        public bool Stopped { get; private set; } = false;
        public bool StoppedImplicitly { get; private set; } = false;

        public bool IsExceptionalHalt { get; private set; } = false;

        [NethereumDocExample(DocSection.EvmSimulator, "exceptional-halt", "The reason a frame halted, on both engines")]
        public EvmHaltReason HaltReason { get; private set; } = EvmHaltReason.None;

        public void MarkExceptionalHalt()
        {
            IsExceptionalHalt = true;
        }

        public void MarkHalted(EvmHaltReason reason)
        {
            HaltReason = reason;
#if EVM_SYNC
            HasExecutionError = true;
#endif
            IsExceptionalHalt = true;
            GasRemaining = 0;
            ProgramResult.IsRevert = true;
            Stop();
        }

        public void Halt(EvmHaltReason reason)
        {
#if EVM_SYNC
            MarkHalted(reason);
#else
            throw new EvmHaltException(reason);
#endif
        }

#if EVM_SYNC
        public bool HasExecutionError { get; private set; } = false;

        public void SetExecutionError()
        {
            MarkHalted(EvmHaltReason.ExceptionalHalt);
        }
#endif
        public byte[] ByteCode { get; }
        public ProgramContext ProgramContext { get; }

        public int GetCurrentInstructionIndex()
        {
            return IndexOfInstructionAt(pc);
        }

        public void Stop()
        {
            Stopped = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Step()
        {
            MoveTo(BytecodeDecoder.NextPc(ByteCode, pc, eip8024Enabled));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int bytes)
        {
            MoveTo(pc + bytes);
        }

        public void Step(int steps)
        {
            if (steps == 1)
            {
                Step();
                return;
            }
            SetInstrunctionIndex(GetCurrentInstructionIndex() + steps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPush(byte[] stackWord)
        {
            if (PushWouldOverflow())
            {
                Halt(EvmHaltReason.StackOverflow);
                return;
            }
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
                Halt(EvmHaltReason.StackUnderflow);
                return new byte[32];
            }
            return stack[actualIndex].ToBigEndian();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256 StackPeekAtU256(int index)
        {
            int actualIndex = stackPointer - 1 - index;
            if (actualIndex < 0 || index < 0)
            {
                Halt(EvmHaltReason.StackUnderflow);
                return EvmUInt256.Zero;
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
                Halt(EvmHaltReason.StackUnderflow);
                return;
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
                Halt(EvmHaltReason.StackUnderflow);
                return;
            }
            var src = stack[stackPointer - index];
            if (PushWouldOverflow())
            {
                Halt(EvmHaltReason.StackOverflow);
                return;
            }
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
                Halt(EvmHaltReason.StackUnderflow);
                return;
            }
            int indexA = stackPointer - 1 - depthFromTopA;
            int indexB = stackPointer - 1 - depthFromTopB;
            var temp = stack[indexA];
            stack[indexA] = stack[indexB];
            stack[indexB] = temp;
        }

        public void RaiseInvalidEip8024Immediate(string opcodeName)
        {
            Halt(EvmHaltReason.InvalidParameter);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] StackPop()
        {
            if (stackPointer == 0)
            {
                Halt(EvmHaltReason.StackUnderflow);
                return new byte[32];
            }
            return stack[--stackPointer].ToBigEndian();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PushWouldOverflow()
        {
            return stackPointer == stack.Length && !TryGrowStack();
        }

        private bool TryGrowStack()
        {
            if (stack.Length >= MAX_STACKSIZE) return false;
            var full = Interlocked.Exchange(ref spareFullStack, null) ?? new EvmUInt256[MAX_STACKSIZE];
            Array.Copy(stack, full, stackPointer);
            stack = full;
            return true;
        }

        internal void ReleaseStack()
        {
            if (stack.Length == MAX_STACKSIZE) Interlocked.CompareExchange(ref spareFullStack, stack, null);
            stack = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VerifyStackOverflow(int args, int returns)
        {
            if (stackPointer - args + returns > MAX_STACKSIZE)
            {
                Halt(EvmHaltReason.StackOverflow);
            }
        }

        public void ExpandMemory(int newSize)
        {
            if (newSize > Memory.Count)
            {
                var gap = newSize - Memory.Count;
                var extraSize = ((gap + 31) / 32) * 32;
                MemoryBuffer.GrowTo(Memory, Memory.Count + extraSize);
            }
        }

        public void WriteToMemory(int index, byte[] data)
        {
            WriteToMemory(index, data.Length, data);
        }

        public void WriteToMemory(int index, int totalSize, byte[] data, bool extend = true)
        {
            if (data == null) data = new byte[0];
            CopyToMemory(index, totalSize, data, 0, data.Length, extend);
        }

        internal void CopyToMemory(int index, int totalSize, byte[] source, int sourceOffset, int sourceCount, bool extend = true)
        {
            if (totalSize == 0) return;

            int newSize = index + totalSize;
            if (extend && newSize > Memory.Count) ExpandMemory(newSize);

            MemoryBuffer.Write(Memory, index, totalSize, source, sourceOffset, sourceCount);
        }

        internal void StoreByteInMemory(int index, byte value)
        {
            if (index >= Memory.Count) ExpandMemory(index + 1);
            MemoryBuffer.WriteByte(Memory, index, value);
        }

        internal void StoreWordInMemory(int index, EvmUInt256 value)
        {
            if (index + 32 > Memory.Count) ExpandMemory(index + 32);
            MemoryBuffer.WriteWord(Memory, index, value);
        }

        internal EvmUInt256 LoadWordFromMemory(int index)
        {
            if (index + 32 > Memory.Count) ExpandMemory(index + 32);
            return MemoryBuffer.ReadWord(Memory, index);
        }

        internal void MoveWithinMemory(int destination, int source, int length)
        {
            MemoryBuffer.Move(Memory, destination, source, length);
        }

        internal byte[] ReadMemory(int index, int length)
        {
            return MemoryBuffer.Read(Memory, index, length);
        }

        internal string ReadMemoryHex(int index, int length)
        {
            return HexText.FromMemory(Memory, index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256 StackPopU256()
        {
            if (stackPointer == 0)
            {
                Halt(EvmHaltReason.StackUnderflow);
                return EvmUInt256.Zero;
            }
            return stack[--stackPointer];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StackPush(EvmUInt256 value)
        {
            if (PushWouldOverflow())
            {
                Halt(EvmHaltReason.StackOverflow);
                return;
            }
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
            if (PushWouldOverflow())
            {
                Halt(EvmHaltReason.StackOverflow);
                return;
            }
            stack[stackPointer++] = value;
        }

        public bool IsAddressWarm(byte[] addressBytes)
        {
            return ProgramContext.ExecutionStateService.AddressIsWarm(EvmAddress.From(addressBytes));
        }

        public void MarkAddressAsWarm(byte[] addressBytes)
        {
            ProgramContext.ExecutionStateService.MarkAddressAsWarm(EvmAddress.From(addressBytes));
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

            if (offset.FitsInInt && length.FitsInInt)
                return CalculateMemoryExpansionGas((long)offset.ToInt(), (long)length.ToInt());

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
            MarkHalted(EvmHaltReason.OutOfGas);
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