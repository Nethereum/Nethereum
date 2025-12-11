using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Nethereum.Ssz
{
    /// <summary>
    /// Minimal SSZ writer that supports the primitive types required by the light client.
    /// </summary>
    public class SszWriter : IDisposable
    {
        private readonly MemoryStream _stream = new MemoryStream();
        private bool _disposed;

        public void WriteBoolean(bool value)
        {
            EnsureNotDisposed();
            _stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteUInt16(ushort value) => WriteLittleEndian(value, sizeof(ushort));

        public void WriteUInt32(uint value) => WriteLittleEndian(value, sizeof(uint));

        public void WriteUInt64(ulong value) => WriteLittleEndian(value, sizeof(ulong));

        private void WriteLittleEndian(ulong value, int size)
        {
            EnsureNotDisposed();
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            _stream.Write(buffer.Slice(0, size));
        }

        public void WriteFixedBytes(ReadOnlySpan<byte> bytes, int expectedLength)
        {
            EnsureNotDisposed();
            if (bytes.Length != expectedLength)
            {
                throw new ArgumentException($"Expected {expectedLength} bytes but received {bytes.Length}.", nameof(bytes));
            }

            _stream.Write(bytes);
        }

        public void WriteVariableBytes(ReadOnlySpan<byte> bytes, ulong? maxLength = null)
        {
            EnsureNotDisposed();
            ValidateLength(bytes.Length, maxLength, nameof(bytes));
            WriteUInt32((uint)bytes.Length);
            _stream.Write(bytes);
        }

        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            EnsureNotDisposed();
            _stream.Write(bytes);
        }

        public void WriteList<T>(IList<T> items, Action<SszWriter, T> writeElement, ulong? maxLength = null)
        {
            EnsureNotDisposed();
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (writeElement == null) throw new ArgumentNullException(nameof(writeElement));

            ValidateLength(items.Count, maxLength, nameof(items));

            foreach (var item in items)
            {
                writeElement(this, item);
            }
        }

        public void WriteVector(IList<byte[]> items, int elementSize, int? expectedElementCount = null)
        {
            EnsureNotDisposed();
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (expectedElementCount.HasValue && items.Count != expectedElementCount.Value)
            {
                throw new ArgumentException($"Vector expected {expectedElementCount.Value} elements but received {items.Count}.", nameof(items));
            }

            foreach (var item in items)
            {
                WriteFixedBytes(item, elementSize);
            }
        }

        public byte[] ToArray()
        {
            EnsureNotDisposed();
            return _stream.ToArray();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _stream.Dispose();
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SszWriter));
            }
        }

        private static void ValidateLength(int actualLength, ulong? maximum, string argumentName)
        {
            if (maximum.HasValue && (ulong)actualLength > maximum.Value)
            {
                throw new ArgumentOutOfRangeException(argumentName, $"Length {actualLength} exceeds SSZ maximum {maximum.Value}.");
            }
        }
    }
}
