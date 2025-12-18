using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Nethereum.Util;

namespace Nethereum.Ssz
{
    /// <summary>
    /// Minimal SSZ Merkleization helpers (SHA-256 based) for vectors and lists.
    /// </summary>
    public static class SszMerkleizer
    {
        private const int ChunkSize = 32;
        private static readonly byte[] ZeroChunk = ByteUtil.InitialiseEmptyByteArray(ChunkSize);

        public static byte[] HashTreeRootVector(IList<byte[]> chunks, ulong length)
        {
            var merkleRoot = Merkleize(chunks);
            return MixInLength(merkleRoot, length);
        }

        public static byte[] HashTreeRootList(IList<byte[]> chunks, ulong length)
        {
            return HashTreeRootVector(chunks, length);
        }

        public static IList<byte[]> Chunkify(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return new List<byte[]> { ByteUtil.InitialiseEmptyByteArray(ChunkSize) };
            }

            var chunkCount = (data.Length + ChunkSize - 1) / ChunkSize;
            var result = new List<byte[]>(chunkCount);
            for (var i = 0; i < chunkCount; i++)
            {
                var chunk = ByteUtil.InitialiseEmptyByteArray(ChunkSize);
                var slice = data.Slice(i * ChunkSize, Math.Min(ChunkSize, data.Length - (i * ChunkSize)));
                slice.CopyTo(chunk);
                result.Add(chunk);
            }

            return result;
        }

        public static byte[] Merkleize(IList<byte[]> chunks)
        {
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));

            if (chunks.Count == 0)
            {
                return ZeroChunk.ToArray();
            }

            var working = NormalizeChunks(chunks);
            var targetSize = 1;
            while (targetSize < working.Count)
            {
                targetSize <<= 1;
            }
            while (working.Count < targetSize)
            {
                working.Add(ByteUtil.InitialiseEmptyByteArray(ChunkSize));
            }

            while (working.Count > 1)
            {
                var next = new List<byte[]>(working.Count / 2);
                for (var i = 0; i < working.Count; i += 2)
                {
                    next.Add(HashPair(working[i], working[i + 1]));
                }

                working = next;
            }

            return working[0];
        }

        public static byte[] MixInLength(byte[] root, ulong length)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (root.Length != ChunkSize) throw new ArgumentException("Root must be 32 bytes.", nameof(root));

            var lengthChunk = ByteUtil.InitialiseEmptyByteArray(ChunkSize);
            BinaryPrimitives.WriteUInt64LittleEndian(lengthChunk, length);
            using var sha = SHA256.Create();
            return sha.ComputeHash(ByteUtil.Merge(root, lengthChunk));
        }

        private static List<byte[]> NormalizeChunks(IList<byte[]> chunks)
        {
            return chunks.Select(chunk =>
            {
                if (chunk == null) throw new ArgumentNullException(nameof(chunks), "Chunk entry cannot be null.");
                if (chunk.Length != ChunkSize)
                {
                    throw new ArgumentException("Chunks must be exactly 32 bytes.", nameof(chunks));
                }

                var copy = ByteUtil.InitialiseEmptyByteArray(ChunkSize);
                Buffer.BlockCopy(chunk, 0, copy, 0, ChunkSize);
                return copy;
            }).ToList();
        }

        private static byte[] HashPair(byte[] left, byte[] right)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(ByteUtil.Merge(left, right));
        }

        public static bool VerifyProof(byte[] leaf, IList<byte[]> branch, int depth, int index, byte[] root)
        {
            if (leaf == null || leaf.Length != ChunkSize) return false;
            if (root == null || root.Length != ChunkSize) return false;
            if (branch == null || branch.Count < depth) return false;

            var current = new byte[ChunkSize];
            Buffer.BlockCopy(leaf, 0, current, 0, ChunkSize);

            for (int i = 0; i < depth; i++)
            {
                var sibling = branch[i];
                if (sibling == null || sibling.Length != ChunkSize) return false;

                if ((index & (1 << i)) != 0)
                {
                    current = HashPair(sibling, current);
                }
                else
                {
                    current = HashPair(current, sibling);
                }
            }

            return current.SequenceEqual(root);
        }
    }
}
