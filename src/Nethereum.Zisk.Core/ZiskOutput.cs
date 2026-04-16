using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Zisk.Core
{
    /// <summary>
    /// Writes results to Zisk's output registers at 0xA0010000.
    ///
    /// Zisk provides 64 output slots (uint32 each). These become the
    /// public outputs of the ZK proof — values that anyone can read
    /// from the proof without re-executing the program.
    ///
    /// Common conventions:
    ///   Slot 0: exit code (0 = success, non-zero = error)
    ///   Slot 1-63: program-specific results
    /// </summary>
    public static unsafe class ZiskOutput
    {
        private static readonly uint* Slots = (uint*)ZiskMemoryMap.OutputBase;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(int slot, uint value)
        {
            Slots[(uint)slot] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetExitCode(uint code)
        {
            Slots[0] = code;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSuccess()
        {
            Slots[0] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetError(uint errorCode)
        {
            Slots[0] = errorCode;
        }

        /// <summary>
        /// Write a 64-bit value across two output slots (little-endian).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetUInt64(int slot, ulong value)
        {
            Slots[(uint)slot] = (uint)value;
            Slots[(uint)(slot + 1)] = (uint)(value >> 32);
        }

        /// <summary>
        /// Write a 256-bit value across 8 output slots (little-endian).
        /// </summary>
        public static void SetBytes32(int startSlot, ReadOnlySpan<byte> value)
        {
            for (int i = 0; i < 8 && i * 4 < value.Length; i++)
            {
                int offset = value.Length - (i + 1) * 4;
                if (offset >= 0)
                {
                    Slots[(uint)(startSlot + i)] =
                        (uint)value[offset] |
                        ((uint)value[offset + 1] << 8) |
                        ((uint)value[offset + 2] << 16) |
                        ((uint)value[offset + 3] << 24);
                }
            }
        }
    }
}
