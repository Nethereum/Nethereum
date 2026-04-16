using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.Util;

namespace Nethereum.EVM.Witness
{
    public static class BinaryWitness
    {
        internal static void WriteString(BinaryWriter w, string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            w.Write((ushort)bytes.Length);
            w.Write(bytes);
        }

        internal static void WriteBytes(BinaryWriter w, byte[] b)
        {
            w.Write((uint)b.Length);
            w.Write(b);
        }

        internal static void WriteBytes32(BinaryWriter w, EvmUInt256 value)
        {
            w.Write(value.ToBigEndian());
        }

        internal static void WriteFixedBytes(BinaryWriter w, byte[] data, int length)
        {
            if (data == null || data.Length == 0)
            {
                w.Write(new byte[length]);
            }
            else if (data.Length >= length)
            {
                w.Write(data, 0, length);
            }
            else
            {
                w.Write(data);
                w.Write(new byte[length - data.Length]);
            }
        }

        internal static byte[] ReadFixedBytes(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset, int length)
        {
            var result = new byte[length];
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            data.Slice(offset, length).CopyTo(result);
#else
            Array.Copy(data, offset, result, 0, length);
#endif
            offset += length;
            return result;
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        internal static ulong ReadU64(ReadOnlySpan<byte> data, ref int offset)
        {
            ulong val = BitConverter.ToUInt64(data.Slice(offset, 8));
            offset += 8;
            return val;
        }

        internal static ushort ReadU16(ReadOnlySpan<byte> data, ref int offset)
        {
            ushort val = BitConverter.ToUInt16(data.Slice(offset, 2));
            offset += 2;
            return val;
        }

        internal static string ReadString(ReadOnlySpan<byte> data, ref int offset)
        {
            ushort len = ReadU16(data, ref offset);
            var str = System.Text.Encoding.UTF8.GetString(data.Slice(offset, len));
            offset += len;
            return str;
        }

        internal static byte[] ReadBytesArray(ReadOnlySpan<byte> data, ref int offset)
        {
            uint len = BitConverter.ToUInt32(data.Slice(offset, 4));
            offset += 4;
            var result = data.Slice(offset, (int)len).ToArray();
            offset += (int)len;
            return result;
        }

        internal static EvmUInt256 ReadBytes32AsEvmUInt256(ReadOnlySpan<byte> data, ref int offset)
        {
            var val = EvmUInt256.FromBigEndian(data.Slice(offset, 32).ToArray());
            offset += 32;
            return val;
#else
        internal static ulong ReadU64(byte[] data, ref int offset)
        {
            ulong val = BitConverter.ToUInt64(data, offset);
            offset += 8;
            return val;
        }

        internal static ushort ReadU16(byte[] data, ref int offset)
        {
            ushort val = BitConverter.ToUInt16(data, offset);
            offset += 2;
            return val;
        }

        internal static string ReadString(byte[] data, ref int offset)
        {
            ushort len = ReadU16(data, ref offset);
            var str = System.Text.Encoding.UTF8.GetString(data, offset, len);
            offset += len;
            return str;
        }

        internal static byte[] ReadBytesArray(byte[] data, ref int offset)
        {
            uint len = BitConverter.ToUInt32(data, offset);
            offset += 4;
            var result = new byte[len];
            Array.Copy(data, offset, result, 0, (int)len);
            offset += (int)len;
            return result;
        }

        internal static EvmUInt256 ReadBytes32AsEvmUInt256(byte[] data, ref int offset)
        {
            var bytes = new byte[32];
            Array.Copy(data, offset, bytes, 0, 32);
            var val = EvmUInt256.FromBigEndian(bytes);
            offset += 32;
            return val;
#endif
        }
    }

    public class WitnessAccount
    {
        public string Address { get; set; }
        public EvmUInt256 Balance { get; set; }
        public long Nonce { get; set; }
        public byte[] Code { get; set; }
        public List<WitnessStorageSlot> Storage { get; set; } = new List<WitnessStorageSlot>();

        // Merkle proofs for stateless verification
        public byte[][] AccountProof { get; set; }
        public byte[] StorageRoot { get; set; }
        public List<WitnessStorageProof> StorageProofs { get; set; }
    }

    public class WitnessStorageSlot
    {
        public EvmUInt256 Key { get; set; }
        public EvmUInt256 Value { get; set; }
    }

    public class WitnessStorageProof
    {
        public EvmUInt256 Key { get; set; }
        public EvmUInt256 Value { get; set; }
        public byte[][] Proof { get; set; }
    }
}
