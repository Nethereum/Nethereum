using System;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.CoreChain.DataAvailability
{
    public static class BlockHashesTree
    {
        private static readonly byte[] DomainTag = new Sha3Keccack()
            .CalculateHash(System.Text.Encoding.UTF8.GetBytes("appchain:v1:blockhashes"));

        public static byte[] ComputeRoot(IReadOnlyList<byte[]> blockHashes)
        {
            if (blockHashes == null || blockHashes.Count == 0)
                return new byte[32];

            var keccak = new Sha3Keccack();
            var leaves = HashLeaves(keccak, blockHashes);

            while (leaves.Count > 1)
                leaves = NextLevel(keccak, leaves);

            return leaves[0];
        }

        public static byte[] ComputeMerkleProof(IReadOnlyList<byte[]> blockHashes, int index,
            out byte[] leaf)
        {
            if (blockHashes == null || index < 0 || index >= blockHashes.Count)
                throw new ArgumentException("Invalid index");

            var keccak = new Sha3Keccack();
            var leaves = HashLeaves(keccak, blockHashes);

            leaf = leaves[index];
            var proof = new List<byte[]>();
            var idx = index;

            while (leaves.Count > 1)
            {
                if (HasSibling(idx, leaves.Count))
                    proof.Add(leaves[idx ^ 1]);

                leaves = NextLevel(keccak, leaves);
                idx /= 2;
            }

            var proofBytes = new byte[proof.Count * 32];
            for (int i = 0; i < proof.Count; i++)
                Array.Copy(proof[i], 0, proofBytes, i * 32, 32);
            return proofBytes;
        }

        public static bool VerifyProof(byte[] root, byte[] blockHash, int index, byte[] proof, int totalLeaves)
        {
            if (root == null || blockHash == null || proof == null || proof.Length % 32 != 0)
                return false;
            if (totalLeaves <= 0 || index < 0 || index >= totalLeaves)
                return false;
            if (proof.Length / 32 != ExpectedProofNodes(index, totalLeaves))
                return false;

            var keccak = new Sha3Keccack();
            var current = HashLeaf(keccak, blockHash);

            var idx = index;
            long levelCount = totalLeaves;
            for (int i = 0; i < proof.Length; i += 32)
            {
                while (levelCount > 1 && !HasSibling(idx, levelCount))
                {
                    idx /= 2;
                    levelCount = (levelCount + 1) / 2;
                }

                var sibling = new byte[32];
                Array.Copy(proof, i, sibling, 0, 32);

                current = idx % 2 == 0
                    ? HashPair(keccak, current, sibling)
                    : HashPair(keccak, sibling, current);
                idx /= 2;
                levelCount = (levelCount + 1) / 2;
            }

            return ByteUtil.AreEqual(current, root);
        }

        private static int ExpectedProofNodes(int index, int totalLeaves)
        {
            var count = 0;
            long idx = index;
            long levelCount = totalLeaves;
            while (levelCount > 1)
            {
                if (HasSibling(idx, levelCount))
                    count++;
                idx /= 2;
                levelCount = (levelCount + 1) / 2;
            }
            return count;
        }

        private static bool HasSibling(long index, long levelCount)
        {
            return (index ^ 1) < levelCount;
        }

        private static byte[] HashLeaf(Sha3Keccack keccak, byte[] hash)
        {
            var tagged = new byte[DomainTag.Length + (hash?.Length ?? 0)];
            Array.Copy(DomainTag, 0, tagged, 0, DomainTag.Length);
            if (hash != null)
                Array.Copy(hash, 0, tagged, DomainTag.Length, hash.Length);
            return keccak.CalculateHash(tagged);
        }

        private static List<byte[]> HashLeaves(Sha3Keccack keccak, IReadOnlyList<byte[]> blockHashes)
        {
            var leaves = new List<byte[]>(blockHashes.Count);
            foreach (var hash in blockHashes)
                leaves.Add(HashLeaf(keccak, hash));
            return leaves;
        }

        private static byte[] HashPair(Sha3Keccack keccak, byte[] left, byte[] right)
        {
            var combined = new byte[64];
            Array.Copy(left, 0, combined, 0, 32);
            Array.Copy(right, 0, combined, 32, 32);
            return keccak.CalculateHash(combined);
        }

        private static List<byte[]> NextLevel(Sha3Keccack keccak, List<byte[]> leaves)
        {
            var next = new List<byte[]>((leaves.Count + 1) / 2);
            for (int i = 0; i < leaves.Count; i += 2)
                next.Add(i + 1 < leaves.Count ? HashPair(keccak, leaves[i], leaves[i + 1]) : leaves[i]);
            return next;
        }
    }
}
