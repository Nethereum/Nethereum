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
            var leaves = new List<byte[]>(blockHashes.Count);

            foreach (var hash in blockHashes)
            {
                var tagged = new byte[DomainTag.Length + (hash?.Length ?? 0)];
                Array.Copy(DomainTag, 0, tagged, 0, DomainTag.Length);
                if (hash != null)
                    Array.Copy(hash, 0, tagged, DomainTag.Length, hash.Length);
                leaves.Add(keccak.CalculateHash(tagged));
            }

            while (leaves.Count > 1)
            {
                var next = new List<byte[]>((leaves.Count + 1) / 2);
                for (int i = 0; i < leaves.Count; i += 2)
                {
                    if (i + 1 < leaves.Count)
                    {
                        var combined = new byte[64];
                        Array.Copy(leaves[i], 0, combined, 0, 32);
                        Array.Copy(leaves[i + 1], 0, combined, 32, 32);
                        next.Add(keccak.CalculateHash(combined));
                    }
                    else
                    {
                        next.Add(leaves[i]);
                    }
                }
                leaves = next;
            }

            return leaves[0];
        }

        public static byte[] ComputeMerkleProof(IReadOnlyList<byte[]> blockHashes, int index,
            out byte[] leaf)
        {
            if (blockHashes == null || index < 0 || index >= blockHashes.Count)
                throw new ArgumentException("Invalid index");

            var keccak = new Sha3Keccack();
            var leaves = new List<byte[]>(blockHashes.Count);

            foreach (var hash in blockHashes)
            {
                var tagged = new byte[DomainTag.Length + (hash?.Length ?? 0)];
                Array.Copy(DomainTag, 0, tagged, 0, DomainTag.Length);
                if (hash != null)
                    Array.Copy(hash, 0, tagged, DomainTag.Length, hash.Length);
                leaves.Add(keccak.CalculateHash(tagged));
            }

            leaf = leaves[index];
            var proof = new List<byte[]>();
            var idx = index;

            while (leaves.Count > 1)
            {
                var next = new List<byte[]>((leaves.Count + 1) / 2);
                for (int i = 0; i < leaves.Count; i += 2)
                {
                    if (i + 1 < leaves.Count)
                    {
                        if (i == idx - (idx % 2))
                            proof.Add(leaves[i + (idx % 2 == 0 ? 1 : 0)]);

                        var combined = new byte[64];
                        Array.Copy(leaves[i], 0, combined, 0, 32);
                        Array.Copy(leaves[i + 1], 0, combined, 32, 32);
                        next.Add(keccak.CalculateHash(combined));
                    }
                    else
                    {
                        next.Add(leaves[i]);
                    }
                }
                idx /= 2;
                leaves = next;
            }

            var proofBytes = new byte[proof.Count * 32];
            for (int i = 0; i < proof.Count; i++)
                Array.Copy(proof[i], 0, proofBytes, i * 32, 32);
            return proofBytes;
        }

        public static bool VerifyProof(byte[] root, byte[] blockHash, int index, byte[] proof, int totalLeaves)
        {
            var keccak = new Sha3Keccack();

            var tagged = new byte[DomainTag.Length + (blockHash?.Length ?? 0)];
            Array.Copy(DomainTag, 0, tagged, 0, DomainTag.Length);
            if (blockHash != null)
                Array.Copy(blockHash, 0, tagged, DomainTag.Length, blockHash.Length);
            var current = keccak.CalculateHash(tagged);

            var idx = index;
            for (int i = 0; i < proof.Length; i += 32)
            {
                var sibling = new byte[32];
                Array.Copy(proof, i, sibling, 0, 32);

                var combined = new byte[64];
                if (idx % 2 == 0)
                {
                    Array.Copy(current, 0, combined, 0, 32);
                    Array.Copy(sibling, 0, combined, 32, 32);
                }
                else
                {
                    Array.Copy(sibling, 0, combined, 0, 32);
                    Array.Copy(current, 0, combined, 32, 32);
                }
                current = keccak.CalculateHash(combined);
                idx /= 2;
            }

            return ByteUtil.AreEqual(current, root);
        }
    }
}
