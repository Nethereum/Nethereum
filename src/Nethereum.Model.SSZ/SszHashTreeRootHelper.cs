using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;
using Nethereum.Util;

namespace Nethereum.Model.SSZ
{
    public static class SszHashTreeRootHelper
    {
        public static byte[] HashTreeRootUint64(ulong value)
        {
            var chunk = new byte[32];
            BinaryPrimitives.WriteUInt64LittleEndian(chunk, value);
            return chunk;
        }

        public static byte[] HashTreeRootUint64(EvmUInt256? value)
        {
            return HashTreeRootUint64(value.HasValue ? (ulong)value.Value : 0UL);
        }

        public static byte[] HashTreeRootUint8(byte value)
        {
            var chunk = new byte[32];
            chunk[0] = value;
            return chunk;
        }

        public static byte[] HashTreeRootBoolean(bool value)
        {
            var chunk = new byte[32];
            chunk[0] = value ? (byte)1 : (byte)0;
            return chunk;
        }

        public static byte[] HashTreeRootUint256(EvmUInt256 value)
        {
            return value.ToLittleEndian();
        }

        public static byte[] HashTreeRootUint256(EvmUInt256? value)
        {
            return (value ?? EvmUInt256.Zero).ToLittleEndian();
        }

        public static byte[] HashTreeRootAddress(string hexAddress)
        {
            var addressBytes = string.IsNullOrEmpty(hexAddress)
                ? new byte[20]
                : hexAddress.HexToByteArray();
            if (addressBytes.Length != 20)
                throw new ArgumentException($"Address must be 20 bytes, got {addressBytes.Length}.");
            var chunks = SszMerkleizer.Chunkify(addressBytes);
            return SszMerkleizer.Merkleize(chunks);
        }

        public static byte[] HashTreeRootBytes32(byte[] value)
        {
            if (value == null || value.Length != 32)
                throw new ArgumentException("Value must be 32 bytes.");
            return value;
        }

        public static byte[] HashTreeRootProgressiveByteList(byte[] data)
        {
            data = data ?? Array.Empty<byte>();
            var chunks = data.Length > 0
                ? SszMerkleizer.Chunkify(data)
                : (IList<byte[]>)new List<byte[]>();
            var root = SszMerkleizer.MerkleizeProgressive(chunks);
            return SszMerkleizer.MixInLength(root, (ulong)data.Length);
        }

        public static byte[] HashTreeRootByteList(byte[] data, int maxBytes)
        {
            data = data ?? Array.Empty<byte>();
            var chunks = data.Length > 0
                ? SszMerkleizer.Chunkify(data)
                : (IList<byte[]>)new List<byte[]>();
            var limit = (maxBytes + 31) / 32;
            var root = SszMerkleizer.Merkleize(chunks, limit);
            return SszMerkleizer.MixInLength(root, (ulong)data.Length);
        }
    }
}
