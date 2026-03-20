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
            var targetSize = NextPowerOfTwo(working.Count);
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

        public static byte[] Merkleize(IList<byte[]> chunks, int limit)
        {
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            if (chunks.Count > limit)
                throw new ArgumentException($"Chunk count {chunks.Count} exceeds limit {limit}.", nameof(chunks));

            if (limit == 0)
            {
                return ZeroChunk.ToArray();
            }

            var working = chunks.Count > 0 ? NormalizeChunks(chunks) : new List<byte[]>();
            var targetSize = NextPowerOfTwo(limit);
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

        public static byte[] MerkleizeProgressive(IList<byte[]> chunks)
        {
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            return MerkleizeProgressiveInner(chunks, 0, chunks.Count, 1);
        }

        private static byte[] MerkleizeProgressiveInner(IList<byte[]> chunks, int offset, int remaining, int numLeaves)
        {
            if (remaining <= 0)
            {
                return ZeroChunk.ToArray();
            }

            var take = Math.Min(numLeaves, remaining);
            var subtreeChunks = new List<byte[]>(take);
            for (var i = 0; i < take; i++)
            {
                subtreeChunks.Add(chunks[offset + i]);
            }

            var subtree = Merkleize(subtreeChunks, numLeaves);
            var rest = MerkleizeProgressiveInner(chunks, offset + take, remaining - take, numLeaves * 4);
            return HashPair(rest, subtree);
        }

        public static byte[] PackActiveFields(bool[] activeFields)
        {
            if (activeFields == null) throw new ArgumentNullException(nameof(activeFields));
            if (activeFields.Length == 0) throw new ArgumentException("Active fields cannot be empty.", nameof(activeFields));
            if (activeFields.Length > 256) throw new ArgumentException("Active fields cannot exceed 256.", nameof(activeFields));

            var chunk = ByteUtil.InitialiseEmptyByteArray(ChunkSize);
            for (var i = 0; i < activeFields.Length; i++)
            {
                if (activeFields[i])
                {
                    chunk[i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return chunk;
        }

        public static byte[] MixInActiveFields(byte[] root, bool[] activeFields)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (root.Length != ChunkSize) throw new ArgumentException("Root must be 32 bytes.", nameof(root));

            var packed = PackActiveFields(activeFields);
            using var sha = SHA256.Create();
            return sha.ComputeHash(ByteUtil.Merge(root, packed));
        }

        public static byte[] HashTreeRootProgressiveList(IList<byte[]> elementRoots)
        {
            if (elementRoots == null) throw new ArgumentNullException(nameof(elementRoots));
            var progressiveRoot = MerkleizeProgressive(elementRoots);
            return MixInLength(progressiveRoot, (ulong)elementRoots.Count);
        }

        public static byte[] HashTreeRootBasicProgressiveList(IList<byte[]> packedChunks, ulong elementCount)
        {
            if (packedChunks == null) throw new ArgumentNullException(nameof(packedChunks));
            var progressiveRoot = MerkleizeProgressive(packedChunks);
            return MixInLength(progressiveRoot, elementCount);
        }

        public static byte[] HashTreeRootProgressiveBitlist(bool[] bits)
        {
            if (bits == null) throw new ArgumentNullException(nameof(bits));
            var chunks = PackBitsToChunks(bits);
            var progressiveRoot = MerkleizeProgressive(chunks);
            return MixInLength(progressiveRoot, (ulong)bits.Length);
        }

        public static IList<byte[]> PackBitsToChunks(bool[] bits)
        {
            if (bits == null) throw new ArgumentNullException(nameof(bits));
            if (bits.Length == 0) return new List<byte[]>();

            var byteCount = (bits.Length + 7) / 8;
            var chunkCount = (byteCount + ChunkSize - 1) / ChunkSize;
            var result = new List<byte[]>(chunkCount);

            for (var c = 0; c < chunkCount; c++)
            {
                var chunk = ByteUtil.InitialiseEmptyByteArray(ChunkSize);
                for (var b = 0; b < ChunkSize; b++)
                {
                    var byteIndex = c * ChunkSize + b;
                    if (byteIndex >= byteCount) break;

                    byte val = 0;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        var bitIndex = byteIndex * 8 + bit;
                        if (bitIndex < bits.Length && bits[bitIndex])
                        {
                            val |= (byte)(1 << bit);
                        }
                    }
                    chunk[b] = val;
                }
                result.Add(chunk);
            }

            return result;
        }

        public static byte[] HashTreeRootProgressiveContainer(IList<byte[]> fieldRoots, bool[] activeFields)
        {
            if (fieldRoots == null) throw new ArgumentNullException(nameof(fieldRoots));
            if (activeFields == null) throw new ArgumentNullException(nameof(activeFields));

            var activeCount = 0;
            for (var i = 0; i < activeFields.Length; i++)
            {
                if (activeFields[i]) activeCount++;
            }

            if (fieldRoots.Count != activeCount)
                throw new ArgumentException(
                    $"Field root count {fieldRoots.Count} does not match active field count {activeCount}.");

            // Expand field roots into all positions — inactive fields get zero-hash entries
            var allPositions = new List<byte[]>(activeFields.Length);
            var fieldIndex = 0;
            for (var i = 0; i < activeFields.Length; i++)
            {
                if (activeFields[i])
                {
                    allPositions.Add(fieldRoots[fieldIndex++]);
                }
                else
                {
                    allPositions.Add(ByteUtil.InitialiseEmptyByteArray(ChunkSize));
                }
            }

            var progressiveRoot = MerkleizeProgressive(allPositions);
            return MixInActiveFields(progressiveRoot, activeFields);
        }

        public static byte[] MixInSelector(byte[] root, byte selector)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (root.Length != ChunkSize) throw new ArgumentException("Root must be 32 bytes.", nameof(root));

            var selectorChunk = ByteUtil.InitialiseEmptyByteArray(ChunkSize);
            selectorChunk[0] = selector;
            using var sha = SHA256.Create();
            return sha.ComputeHash(ByteUtil.Merge(root, selectorChunk));
        }

        public static byte[] HashTreeRootCompatibleUnion(byte[] dataRoot, byte selector)
        {
            return MixInSelector(dataRoot, selector);
        }

        private static int NextPowerOfTwo(int value)
        {
            if (value <= 0) return 1;
            var result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
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
