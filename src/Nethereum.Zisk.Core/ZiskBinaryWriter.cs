using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Zisk.Core
{
    /// <summary>
    /// Binary writer for creating Zisk witness/input files on the host side.
    ///
    /// Produces data in the format expected by ZiskBinaryReader.
    /// All multi-byte integers are little-endian.
    /// Strings are length-prefixed with a u16 byte count.
    ///
    /// This writer is used on the HOST (not inside Zisk) to prepare
    /// input files that will be fed to the zkVM via --legacy-inputs or -i.
    /// </summary>
    public class ZiskBinaryWriter
    {
        private byte[] _buffer;
        private int _position;

        public ZiskBinaryWriter(int initialCapacity = 4096)
        {
            _buffer = new byte[initialCapacity];
            _position = 0;
        }

        public int Length => _position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value)
        {
            EnsureCapacity(2);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value)
        {
            EnsureCapacity(4);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value)
        {
            EnsureCapacity(8);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
            _buffer[_position++] = (byte)(value >> 32);
            _buffer[_position++] = (byte)(value >> 40);
            _buffer[_position++] = (byte)(value >> 48);
            _buffer[_position++] = (byte)(value >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        public void WriteBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            EnsureCapacity(data.Length);
            Array.Copy(data, 0, _buffer, _position, data.Length);
            _position += data.Length;
        }

        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            EnsureCapacity(data.Length);
            data.CopyTo(_buffer.AsSpan(_position));
            _position += data.Length;
        }

        /// <summary>
        /// Write a length-prefixed string. Format: [u16 byteLength] [ascii bytes].
        /// </summary>
        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteUInt16(0);
                return;
            }
            WriteUInt16((ushort)value.Length);
            EnsureCapacity(value.Length);
            for (int i = 0; i < value.Length; i++)
                _buffer[_position++] = (byte)value[i];
        }

        /// <summary>
        /// Write a length-prefixed byte array. Format: [u16 length] [bytes].
        /// </summary>
        public void WriteLengthPrefixedBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                WriteUInt16(0);
                return;
            }
            WriteUInt16((ushort)data.Length);
            WriteBytes(data);
        }

        /// <summary>
        /// Write a u32-length-prefixed byte array. Format: [u32 length] [bytes].
        /// </summary>
        public void WriteLengthPrefixedBytes32(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                WriteUInt32(0);
                return;
            }
            WriteUInt32((uint)data.Length);
            WriteBytes(data);
        }

        /// <summary>
        /// Get the written data as a byte array.
        /// </summary>
        public byte[] ToArray()
        {
            var result = new byte[_position];
            Array.Copy(_buffer, 0, result, 0, _position);
            return result;
        }

        /// <summary>
        /// Write the data in LEGACY input format for ziskemu --legacy-inputs.
        /// Format: [u64 zero] [u64 data_length] [data]
        /// </summary>
        public byte[] ToLegacyInputFormat()
        {
            int dataLen = _position;
            var result = new byte[16 + dataLen];
            // First u64 = 0 (ignored)
            // Second u64 = data length
            result[8] = (byte)dataLen;
            result[9] = (byte)(dataLen >> 8);
            result[10] = (byte)(dataLen >> 16);
            result[11] = (byte)(dataLen >> 24);
            // Data
            Array.Copy(_buffer, 0, result, 16, dataLen);
            return result;
        }

        /// <summary>
        /// Write the data in STANDARD input format for cargo-zisk prove -i.
        /// Format: [u64 total_size] [u64 zero] [u64 data_length] [data]
        /// </summary>
        public byte[] ToStandardInputFormat()
        {
            int dataLen = _position;
            int legacySize = 16 + dataLen;
            var result = new byte[8 + legacySize];
            // First u64 = legacy file size
            result[0] = (byte)legacySize;
            result[1] = (byte)(legacySize >> 8);
            result[2] = (byte)(legacySize >> 16);
            result[3] = (byte)(legacySize >> 24);
            // Second u64 = 0 (legacy ignored field)
            // Third u64 = data length
            result[16] = (byte)dataLen;
            result[17] = (byte)(dataLen >> 8);
            result[18] = (byte)(dataLen >> 16);
            result[19] = (byte)(dataLen >> 24);
            // Data
            Array.Copy(_buffer, 0, result, 24, dataLen);
            return result;
        }

        private void EnsureCapacity(int additional)
        {
            int required = _position + additional;
            if (required <= _buffer.Length) return;
            int newSize = _buffer.Length;
            while (newSize < required) newSize *= 2;
            var newBuffer = new byte[newSize];
            Array.Copy(_buffer, 0, newBuffer, 0, _position);
            _buffer = newBuffer;
        }
    }
}
