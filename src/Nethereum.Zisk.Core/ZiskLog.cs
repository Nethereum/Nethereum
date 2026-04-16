using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Zisk.Core
{
    /// <summary>
    /// Debug logging via Zisk's UART at 0xA0000200.
    ///
    /// Writes single bytes to the UART address. In ziskemu, this output
    /// appears on the host console. In proving mode, UART writes are
    /// part of the execution trace but do not affect the proof output.
    ///
    /// Note: String.Format, interpolation, and ToString() on numeric types
    /// pull in System.Number formatting code which increases binary size.
    /// Use WriteLong/WriteHex for numeric output without ToString().
    /// </summary>
    public static unsafe class ZiskLog
    {
        private static readonly byte* Uart = (byte*)ZiskMemoryMap.UartAddress;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(char c)
        {
            *Uart = unchecked((byte)c);
        }

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

        public static void WriteLine()
        {
            Write('\n');
        }

        /// <summary>
        /// Write a long value as decimal ASCII without using ToString().
        /// ToString() pulls in System.Number.BigInteger from CoreLib
        /// which adds ~50KB of code to the binary.
        /// </summary>
        public static void WriteLong(long value)
        {
            if (value < 0)
            {
                Write('-');
                value = -value;
            }
            if (value == 0)
            {
                Write('0');
                return;
            }
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

        /// <summary>
        /// Write a byte array as hex without using BitConverter or ToString("x2").
        /// </summary>
        public static void WriteHex(ReadOnlySpan<byte> data)
        {
            Write('0');
            Write('x');
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                *Uart = (byte)HexChar(b >> 4);
                *Uart = (byte)HexChar(b & 0xF);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char HexChar(int nibble)
        {
            return (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);
        }
    }
}
