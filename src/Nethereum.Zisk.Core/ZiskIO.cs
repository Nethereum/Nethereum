using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Zisk.Core
{
    public static unsafe class ZiskIO
    {
        private static readonly byte* Input = (byte*)0x4000_0000UL;
        private static readonly uint* Output = (uint*)0xa001_0000UL;
        private static readonly byte* Uart = (byte*)0xa000_0200UL;

        private static ulong _inputPosition = sizeof(ulong);

        public static ReadOnlySpan<byte> ReadInputLegacy()
        {
            ulong size = *(ulong*)(Input + sizeof(ulong));
            if (size > int.MaxValue)
                Environment.FailFast("Input size exceeds maximum");
            return new ReadOnlySpan<byte>(Input + 2 * sizeof(ulong), (int)size);
        }

        public static ReadOnlySpan<byte> ReadInput()
        {
            byte* data = Input + checked((nint)_inputPosition);
            ulong len = *(ulong*)data;
            if (len > int.MaxValue)
                Environment.FailFast("Input size exceeds maximum");
            ulong alignedLen = (len + 7UL) & ~7UL;
            _inputPosition = checked(_inputPosition + sizeof(ulong) + alignedLen);
            return new ReadOnlySpan<byte>(data + sizeof(ulong), (int)len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetOutput(int id, uint value)
        {
            if ((uint)id >= 64U)
                Environment.FailFast("Output id must be 0-63");
            Output[(uint)id] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(char value) => *Uart = unchecked((byte)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(string value)
        {
            if (value is null) return;
            for (int i = 0; i < value.Length; i++)
                *Uart = unchecked((byte)value[i]);
        }

        public static void WriteLine(string value)
        {
            Write(value);
            Write('\n');
        }

        public static void WriteLong(long value)
        {
            if (value < 0) { Write('-'); value = -value; }
            if (value == 0) { Write('0'); return; }
            Span<byte> buf = stackalloc byte[20];
            int pos = 19;
            while (value > 0)
            {
                buf[pos--] = (byte)('0' + (value % 10));
                value /= 10;
            }
            for (int i = pos + 1; i < 20; i++)
                *Uart = buf[i];
        }
    }
}
