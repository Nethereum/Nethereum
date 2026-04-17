using System;
using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.StateDiff
{
    public static class BinaryTrieStateDiffEncoder
    {
        private const int HASH_SIZE = 32;
        private const int STEM_SIZE = BinaryTrieConstants.StemSize;
        private const byte FLAG_HAS_OLD = 0x01;
        private const byte FLAG_HAS_NEW = 0x02;

        public static byte[] Encode(BinaryTrieStateDiff diff)
        {
            if (diff == null) throw new ArgumentNullException(nameof(diff));

            var size = CalculateSize(diff);
            var buffer = new byte[size];
            int offset = 0;

            buffer[offset++] = diff.Version;
            WriteUInt64(buffer, ref offset, (ulong)diff.BlockNumber);
            WriteBytes(buffer, ref offset, diff.PreStateRoot, HASH_SIZE);
            WriteBytes(buffer, ref offset, diff.PostStateRoot, HASH_SIZE);

            WriteUInt32(buffer, ref offset, (uint)diff.StemDiffs.Count);
            foreach (var stemDiff in diff.StemDiffs)
            {
                WriteBytes(buffer, ref offset, stemDiff.Stem, STEM_SIZE);
                WriteUInt16(buffer, ref offset, (ushort)stemDiff.SuffixDiffs.Count);
                foreach (var suffixDiff in stemDiff.SuffixDiffs)
                {
                    buffer[offset++] = suffixDiff.SuffixIndex;
                    byte flags = 0;
                    if (suffixDiff.OldValue != null) flags |= FLAG_HAS_OLD;
                    if (suffixDiff.NewValue != null) flags |= FLAG_HAS_NEW;
                    buffer[offset++] = flags;

                    if (suffixDiff.OldValue != null)
                        WriteBytes(buffer, ref offset, suffixDiff.OldValue, HASH_SIZE);
                    if (suffixDiff.NewValue != null)
                        WriteBytes(buffer, ref offset, suffixDiff.NewValue, HASH_SIZE);
                }
            }

            WriteUInt32(buffer, ref offset, (uint)diff.ProofSiblings.Count);
            foreach (var sibling in diff.ProofSiblings)
                WriteBytes(buffer, ref offset, sibling, HASH_SIZE);

            var result = new byte[offset];
            Array.Copy(buffer, result, offset);
            return result;
        }

        public static BinaryTrieStateDiff Decode(byte[] data)
        {
            if (data == null || data.Length < 73)
                throw new ArgumentException("Invalid BinaryTrieStateDiff data");

            int offset = 0;
            var diff = new BinaryTrieStateDiff();

            diff.Version = data[offset++];
            diff.BlockNumber = (long)ReadUInt64(data, ref offset);
            diff.PreStateRoot = ReadFixedBytes(data, ref offset, HASH_SIZE);
            diff.PostStateRoot = ReadFixedBytes(data, ref offset, HASH_SIZE);

            uint stemCount = ReadUInt32(data, ref offset);
            for (uint i = 0; i < stemCount; i++)
            {
                var stemDiff = new StemDiff();
                stemDiff.Stem = ReadFixedBytes(data, ref offset, STEM_SIZE);

                ushort suffixCount = ReadUInt16(data, ref offset);
                for (ushort j = 0; j < suffixCount; j++)
                {
                    var suffixDiff = new SuffixDiff();
                    suffixDiff.SuffixIndex = data[offset++];
                    byte flags = data[offset++];

                    if ((flags & FLAG_HAS_OLD) != 0)
                        suffixDiff.OldValue = ReadFixedBytes(data, ref offset, HASH_SIZE);
                    if ((flags & FLAG_HAS_NEW) != 0)
                        suffixDiff.NewValue = ReadFixedBytes(data, ref offset, HASH_SIZE);

                    stemDiff.SuffixDiffs.Add(suffixDiff);
                }
                diff.StemDiffs.Add(stemDiff);
            }

            uint siblingCount = ReadUInt32(data, ref offset);
            for (uint i = 0; i < siblingCount; i++)
                diff.ProofSiblings.Add(ReadFixedBytes(data, ref offset, HASH_SIZE));

            return diff;
        }

        private static int CalculateSize(BinaryTrieStateDiff diff)
        {
            int size = 1 + 8 + HASH_SIZE + HASH_SIZE + 4;

            foreach (var stemDiff in diff.StemDiffs)
            {
                size += STEM_SIZE + 2;
                foreach (var s in stemDiff.SuffixDiffs)
                {
                    size += 1 + 1;
                    if (s.OldValue != null) size += HASH_SIZE;
                    if (s.NewValue != null) size += HASH_SIZE;
                }
            }

            size += 4;
            size += diff.ProofSiblings.Count * HASH_SIZE;
            return size;
        }

        private static void WriteUInt64(byte[] buf, ref int offset, ulong value)
        {
            buf[offset] = (byte)value; buf[offset + 1] = (byte)(value >> 8);
            buf[offset + 2] = (byte)(value >> 16); buf[offset + 3] = (byte)(value >> 24);
            buf[offset + 4] = (byte)(value >> 32); buf[offset + 5] = (byte)(value >> 40);
            buf[offset + 6] = (byte)(value >> 48); buf[offset + 7] = (byte)(value >> 56);
            offset += 8;
        }

        private static void WriteUInt32(byte[] buf, ref int offset, uint value)
        {
            buf[offset] = (byte)value; buf[offset + 1] = (byte)(value >> 8);
            buf[offset + 2] = (byte)(value >> 16); buf[offset + 3] = (byte)(value >> 24);
            offset += 4;
        }

        private static void WriteUInt16(byte[] buf, ref int offset, ushort value)
        {
            buf[offset] = (byte)value; buf[offset + 1] = (byte)(value >> 8);
            offset += 2;
        }

        private static void WriteBytes(byte[] buf, ref int offset, byte[] value, int fixedLen)
        {
            if (value != null && value.Length >= fixedLen)
                Array.Copy(value, 0, buf, offset, fixedLen);
            offset += fixedLen;
        }

        private static ulong ReadUInt64(byte[] buf, ref int offset)
        {
            ulong v = buf[offset] | ((ulong)buf[offset + 1] << 8) | ((ulong)buf[offset + 2] << 16)
                | ((ulong)buf[offset + 3] << 24) | ((ulong)buf[offset + 4] << 32)
                | ((ulong)buf[offset + 5] << 40) | ((ulong)buf[offset + 6] << 48)
                | ((ulong)buf[offset + 7] << 56);
            offset += 8; return v;
        }

        private static uint ReadUInt32(byte[] buf, ref int offset)
        {
            uint v = buf[offset] | ((uint)buf[offset + 1] << 8)
                | ((uint)buf[offset + 2] << 16) | ((uint)buf[offset + 3] << 24);
            offset += 4; return v;
        }

        private static ushort ReadUInt16(byte[] buf, ref int offset)
        {
            ushort v = (ushort)(buf[offset] | (buf[offset + 1] << 8));
            offset += 2; return v;
        }

        private static byte[] ReadFixedBytes(byte[] buf, ref int offset, int len)
        {
            var result = new byte[len];
            Array.Copy(buf, offset, result, 0, len);
            offset += len; return result;
        }
    }
}
