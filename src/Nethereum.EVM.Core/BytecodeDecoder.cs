using System;

namespace Nethereum.EVM
{
    internal static class BytecodeDecoder
    {
        private const byte JumpDest = (byte)Instruction.JUMPDEST;
        private const byte Push0 = (byte)Instruction.PUSH0;
        private const byte Push1 = (byte)Instruction.PUSH1;
        private const byte Push32 = (byte)Instruction.PUSH32;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool IsPush(byte opcode)
        {
            return opcode >= Push1 && opcode <= Push32;
        }

        internal static int PushDataLength(byte opcode)
        {
            return opcode - Push0;
        }

        internal static byte ImmediateByteAt(byte[] code, int pc)
        {
            return pc + 1 < code.Length ? code[pc + 1] : (byte)0;
        }

        internal static byte[] ImmediateBytes(byte[] code, int pc, bool eip8024Enabled)
        {
            var opcode = code[pc];
            if (IsPush(opcode))
            {
                var data = new byte[PushDataLength(opcode)];
                Array.Copy(code, pc + 1, data, 0, Math.Min(data.Length, code.Length - pc - 1));
                return data;
            }
            if (eip8024Enabled && Eip8024ImmediateCodec.IsStackAccessOpcode(opcode))
            {
                return new[] { ImmediateByteAt(code, pc) };
            }
            return null;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static int ImmediateLength(byte[] code, int pc, bool eip8024Enabled)
        {
            var opcode = code[pc];
            if (IsPush(opcode))
            {
                return PushDataLength(opcode);
            }
            if (eip8024Enabled && Eip8024ImmediateCodec.IsStackAccessOpcode(opcode))
            {
                return Eip8024ImmediateCodec.IsImmediateValid(opcode, ImmediateByteAt(code, pc)) ? 1 : 0;
            }
            return 0;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static int NextPc(byte[] code, int pc, bool eip8024Enabled)
        {
            return pc + 1 + ImmediateLength(code, pc, eip8024Enabled);
        }

        internal static byte[] BuildPushDataBitmap(byte[] code)
        {
            var bitmap = new byte[code.Length / 8 + 1];
            var pc = 0;
            while (pc < code.Length)
            {
                var opcode = code[pc];
                if (!IsPush(opcode))
                {
                    pc++;
                    continue;
                }
                var dataLength = PushDataLength(opcode);
                var lastDataByte = pc + dataLength < code.Length ? pc + dataLength : code.Length - 1;
                for (var data = pc + 1; data <= lastDataByte; data++)
                {
                    bitmap[data >> 3] |= (byte)(1 << (data & 7));
                }
                pc += 1 + dataLength;
            }
            return bitmap;
        }

        internal static bool IsPushData(byte[] pushDataBitmap, int pc)
        {
            return (pushDataBitmap[pc >> 3] & (1 << (pc & 7))) != 0;
        }

        internal static bool IsJumpDestination(byte[] code, byte[] pushDataBitmap, int pc)
        {
            return pc >= 0 && pc < code.Length && code[pc] == JumpDest && !IsPushData(pushDataBitmap, pc);
        }
    }
}
