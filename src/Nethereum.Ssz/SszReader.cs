using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Nethereum.Ssz
{
    /// <summary>
    /// Minimal SSZ reader companion to <see cref="SszWriter"/>.
    /// </summary>
    public ref struct SszReader
    {
        private ReadOnlySpan<byte> _data;
        private int _offset;

        public SszReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        public bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        public ushort ReadUInt16()
        {
            const int size = sizeof(ushort);
            EnsureRemaining(size);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_offset, size));
            _offset += size;
            return value;
        }

        public uint ReadUInt32()
        {
            const int size = sizeof(uint);
            EnsureRemaining(size);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_offset, size));
            _offset += size;
            return value;
        }

        public ulong ReadUInt64()
        {
            const int size = 8;
            EnsureRemaining(size);
            var value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(_offset, size));
            _offset += size;
            return value;
        }

        public byte[] ReadFixedBytes(int length)
        {
            EnsureRemaining(length);
            var buffer = _data.Slice(_offset, length).ToArray();
            _offset += length;
            return buffer;
        }

        public byte[] ReadVariableBytes(ulong? maxLength = null)
        {
            var length = (int)ReadUInt32();
            if (maxLength.HasValue && (ulong)length > maxLength.Value)
            {
                throw new InvalidOperationException($"Variable length {length} exceeds SSZ maximum {maxLength.Value}.");
            }
            EnsureRemaining(length);
            var buffer = _data.Slice(_offset, length).ToArray();
            _offset += length;
            return buffer;
        }

        public static T[] ReadList<T>(ref SszReader reader, int count)
        {
            var elementReader = SszElementReaderRegistry.Get<T>();
            return ReadList(ref reader, count, elementReader);
        }

        public static T[] ReadList<T>(ref SszReader reader, int count, ISszElementReader<T> elementReader)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (elementReader == null) throw new ArgumentNullException(nameof(elementReader));

            var result = new T[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = elementReader.Read(ref reader);
            }

            return result;
        }

        public byte[][] ReadVector(int elementCount, int elementSize)
        {
            var result = new byte[elementCount][];
            for (var i = 0; i < elementCount; i++)
            {
                result[i] = ReadFixedBytes(elementSize);
            }

            return result;
        }

        public byte[] ReadRemaining()
        {
            var buffer = _data.Slice(_offset).ToArray();
            _offset = _data.Length;
            return buffer;
        }

        private byte ReadByte()
        {
            EnsureRemaining(1);
            return _data[_offset++];
        }

        private void EnsureRemaining(int count)
        {
            if (_offset + count > _data.Length)
            {
                throw new InvalidOperationException("Attempted to read past the end of the SSZ buffer.");
            }
        }
    }
}
