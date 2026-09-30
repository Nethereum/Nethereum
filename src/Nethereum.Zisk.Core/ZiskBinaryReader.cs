using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethereum.Zisk.Core
{
    /// <summary>
    /// Zero-allocation binary reader for Zisk witness data.
    ///
    /// Reads from a ReadOnlySpan without heap allocations.
    /// All multi-byte integers are little-endian (Zisk native order).
    /// Strings are length-prefixed with a u16 byte count.
    /// Fixed-size byte arrays (e.g. 32-byte hashes) use ReadBytes.
    /// </summary>
    public ref struct ZiskBinaryReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public ZiskBinaryReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public int Position => _position;
        public int Remaining => _data.Length - _position;
        public bool HasData => _position < _data.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            return _data[_position++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            ushort value = MemoryMarshal.Read<ushort>(_data.Slice(_position));
            _position += 2;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            uint value = MemoryMarshal.Read<uint>(_data.Slice(_position));
            _position += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            ulong value = MemoryMarshal.Read<ulong>(_data.Slice(_position));
            _position += 8;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64()
        {
            long value = MemoryMarshal.Read<long>(_data.Slice(_position));
            _position += 8;
            return value;
        }

        /// <summary>
        /// Read exactly 'length' bytes and return as a new byte array.
        /// </summary>
        public byte[] ReadBytes(int length)
        {
            var result = new byte[length];
            _data.Slice(_position, length).CopyTo(result);
            _position += length;
            return result;
        }

        /// <summary>
        /// Read exactly 32 bytes (common for hashes and uint256 values).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytes32()
        {
            return ReadBytes(32);
        }

        /// <summary>
        /// Read exactly 20 bytes (common for Ethereum addresses).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytes20()
        {
            return ReadBytes(20);
        }

        /// <summary>
        /// Read a span of bytes without copying. The span is only valid
        /// while the underlying data is valid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadSpan(int length)
        {
            var span = _data.Slice(_position, length);
            _position += length;
            return span;
        }

        /// <summary>
        /// Read a length-prefixed string. Format: [u16 byteLength] [utf8 bytes].
        /// </summary>
        public string ReadString()
        {
            ushort byteLen = ReadUInt16();
            if (byteLen == 0)
                return string.Empty;
            var bytes = _data.Slice(_position, byteLen);
            _position += byteLen;
            return StringFromUtf8(bytes);
        }

        /// <summary>
        /// Read a length-prefixed byte array. Format: [u16 length] [bytes].
        /// </summary>
        public byte[] ReadLengthPrefixedBytes()
        {
            ushort length = ReadUInt16();
            if (length == 0)
                return Array.Empty<byte>();
            return ReadBytes(length);
        }

        /// <summary>
        /// Read a u32-length-prefixed byte array. Format: [u32 length] [bytes].
        /// </summary>
        public byte[] ReadLengthPrefixedBytes32()
        {
            uint length = ReadUInt32();
            if (length == 0)
                return Array.Empty<byte>();
            return ReadBytes((int)length);
        }

        /// <summary>
        /// Skip 'count' bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int count)
        {
            _position += count;
        }

        private static string StringFromUtf8(ReadOnlySpan<byte> bytes)
        {
            var chars = new char[bytes.Length];
            int len = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b == 0) break;
                chars[len++] = (char)b;
            }
            return new string(chars, 0, len);
        }
    }
}
