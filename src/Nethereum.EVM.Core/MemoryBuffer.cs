using System;
using System.Collections.Generic;
using Nethereum.Util;
#if NET8_0_OR_GREATER
using System.Buffers.Binary;
using System.Runtime.InteropServices;
#endif

namespace Nethereum.EVM
{
    internal static class MemoryBuffer
    {
        public static void GrowTo(List<byte> memory, int newCount)
        {
#if NET8_0_OR_GREATER
            int oldCount = memory.Count;
            if (memory.Capacity < newCount) memory.Capacity = Math.Max(newCount, memory.Capacity * 2);
            CollectionsMarshal.SetCount(memory, newCount);
            CollectionsMarshal.AsSpan(memory).Slice(oldCount).Clear();
#else
            memory.AddRange(new byte[newCount - memory.Count]);
#endif
        }

        public static void Write(List<byte> memory, int index, int totalSize, byte[] source, int sourceOffset, int sourceCount)
        {
#if NET8_0_OR_GREATER
            var target = CollectionsMarshal.AsSpan(memory).Slice(index, totalSize);
            int copied = Math.Min(sourceCount, totalSize);
            source.AsSpan(sourceOffset, copied).CopyTo(target);
            target.Slice(copied).Clear();
#else
            for (int i = 0; i < totalSize; i++)
                memory[index + i] = i < sourceCount ? source[sourceOffset + i] : (byte)0;
#endif
        }

        public static void WriteByte(List<byte> memory, int index, byte value)
        {
#if NET8_0_OR_GREATER
            CollectionsMarshal.AsSpan(memory)[index] = value;
#else
            memory[index] = value;
#endif
        }

        public static void WriteWord(List<byte> memory, int index, EvmUInt256 value)
        {
#if NET8_0_OR_GREATER
            var target = CollectionsMarshal.AsSpan(memory).Slice(index, 32);
            BinaryPrimitives.WriteUInt64BigEndian(target, value.U3);
            BinaryPrimitives.WriteUInt64BigEndian(target.Slice(8), value.U2);
            BinaryPrimitives.WriteUInt64BigEndian(target.Slice(16), value.U1);
            BinaryPrimitives.WriteUInt64BigEndian(target.Slice(24), value.U0);
#else
            var word = value.ToBigEndian();
            Write(memory, index, 32, word, 0, 32);
#endif
        }

        public static EvmUInt256 ReadWord(List<byte> memory, int index)
        {
#if NET8_0_OR_GREATER
            var source = CollectionsMarshal.AsSpan(memory).Slice(index, 32);
            return new EvmUInt256(
                BinaryPrimitives.ReadUInt64BigEndian(source),
                BinaryPrimitives.ReadUInt64BigEndian(source.Slice(8)),
                BinaryPrimitives.ReadUInt64BigEndian(source.Slice(16)),
                BinaryPrimitives.ReadUInt64BigEndian(source.Slice(24)));
#else
            return EvmUInt256.FromBigEndian(Read(memory, index, 32));
#endif
        }

        public static void Move(List<byte> memory, int destination, int source, int length)
        {
#if NET8_0_OR_GREATER
            var span = CollectionsMarshal.AsSpan(memory);
            span.Slice(source, length).CopyTo(span.Slice(destination, length));
#else
            Write(memory, destination, length, Read(memory, source, length), 0, length);
#endif
        }

        public static byte[] Read(List<byte> memory, int index, int length)
        {
            var result = new byte[length];
            memory.CopyTo(index, result, 0, length);
            return result;
        }
    }
}
