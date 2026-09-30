using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Zisk.Core
{
    /// <summary>
    /// Reads input data from Zisk's memory-mapped input region at 0x40000000.
    ///
    /// Zisk supports two input formats, determined by how the host provides data:
    ///
    /// LEGACY FORMAT (ziskemu --legacy-inputs):
    ///   [u64 ignored] [u64 data_length] [data...]
    ///   Total file size = 16 + data_length (padded to 8-byte boundary)
    ///   Used by: ziskemu --legacy-inputs
    ///
    /// STANDARD FORMAT (ziskemu -i / cargo-zisk prove -i):
    ///   [u64 total_size] [u64 ignored] [u64 data_length] [data...]
    ///   The file is the legacy content prefixed with a u64 total_size header.
    ///   Used by: ziskemu -i, cargo-zisk prove -i, cargo-zisk stats -i
    ///   Created by: cargo-zisk convert-input
    ///
    /// Both formats map to 0x40000000. The difference is the 8-byte offset.
    /// Use ReadLegacy() for --legacy-inputs, ReadStandard() for -i.
    /// Use Read() to auto-detect based on the first two u64 values.
    /// </summary>
    public static unsafe class ZiskInput
    {
        private static readonly byte* Base = (byte*)ZiskMemoryMap.InputBase;

        /// <summary>
        /// Auto-detect format and read the input data.
        ///
        /// Standard format (-i with convert-input):
        ///   Memory at Base = converted file directly:
        ///   [u64 original_size] [original_file_content...]
        ///
        /// Legacy format (--legacy-inputs):
        ///   Memory at Base = [u64 zero] [u64 file_size] [file_content...]
        ///   File content = [u64 zero] [u64 data_length] [data...]
        ///
        /// Both formats have Base+0=0 (ziskemu header).
        /// Legacy: Base+16=0 (file's own zero header).
        /// Standard: Base+16=non-zero (actual data starts here).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> Read()
        {
            ulong thirdWord = *(ulong*)(Base + 16);
            if (thirdWord == 0)
                return ReadLegacy();
            return ReadStandard();
        }

        /// <summary>
        /// Read input in legacy format (ziskemu --legacy-inputs).
        ///
        /// ziskemu maps legacy input at 0x40000000 as:
        ///   [u64 zero] [u64 file_size] [file_content...]
        ///
        /// The file itself has its own header:
        ///   [u64 ignored=0] [u64 data_length] [data...]
        ///
        /// So the actual data starts at Base+32:
        ///   Base+0:  u64 zero (ziskemu header)
        ///   Base+8:  u64 file_size (ziskemu sets this)
        ///   Base+16: u64 zero (file's ignored field)
        ///   Base+24: u64 data_length (from the file)
        ///   Base+32: actual data
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> ReadLegacy()
        {
            ulong length = *(ulong*)(Base + 24);
            if (length == 0 || length > int.MaxValue)
                return ReadOnlySpan<byte>.Empty;
            return new ReadOnlySpan<byte>(Base + 32, (int)length);
        }

        /// <summary>
        /// Read input in standard format (ziskemu -i / cargo-zisk prove -i).
        ///
        /// cargo-zisk convert-input produces:
        ///   [u64 original_file_size] [original_file_content...]
        ///
        /// ziskemu maps at Base:
        ///   Base+0:  u64 zero (ziskemu always puts zero here)
        ///   Base+8:  u64 original_file_size
        ///   Base+16: original file content (the actual data)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> ReadStandard()
        {
            ulong length = *(ulong*)(Base + 8);
            if (length == 0 || length > int.MaxValue)
                return ReadOnlySpan<byte>.Empty;
            return new ReadOnlySpan<byte>(Base + 16, (int)length);
        }

        /// <summary>
        /// Read raw bytes at an arbitrary offset from the input base.
        /// For programs that define their own input layout.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> ReadRaw(int offset, int length)
        {
            return new ReadOnlySpan<byte>(Base + offset, length);
        }

        /// <summary>
        /// Read a u64 value at a specific byte offset from input base.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64(int offset)
        {
            return *(ulong*)(Base + offset);
        }
    }
}
