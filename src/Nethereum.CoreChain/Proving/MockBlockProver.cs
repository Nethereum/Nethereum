using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.CoreChain.Proving
{
    public class MockBlockProver : IBlockProver
    {
        public const int Groth16ProofSize = 256;

        private static readonly byte[] MockElfHash;

        static MockBlockProver()
        {
            using (var sha = SHA256.Create())
                MockElfHash = sha.ComputeHash(Encoding.UTF8.GetBytes("mock-elf-zisk-v1"));
        }

        public Task<BlockProofResult> ProveBlockAsync(byte[] witnessBytes,
            byte[] preStateRoot, byte[] postStateRoot, long blockNumber)
        {
            byte[] witnessHash;
            byte[] blockHash;
            byte[] proofBytes;
            using (var sha = SHA256.Create())
            {
                witnessHash = sha.ComputeHash(witnessBytes);
                proofBytes = BuildGroth16MockProof(preStateRoot, postStateRoot, blockNumber, witnessHash);
                blockHash = sha.ComputeHash(proofBytes);
            }

            return Task.FromResult(new BlockProofResult
            {
                ProofBytes = proofBytes,
                PreStateRoot = preStateRoot,
                PostStateRoot = postStateRoot,
                ProverComputedStateRoot = postStateRoot,
                ProverComputedBlockHash = blockHash,
                StateRootVerified = true,
                BlockHashVerified = true,
                BlockNumber = blockNumber,
                WitnessHash = witnessHash,
                ElfHash = MockElfHash,
                ProverMode = "Mock"
            });
        }

        /// <summary>
        /// Produces a 256-byte Groth16-shaped proof artifact.
        /// Layout: π_a (64 bytes, G1) + π_b (128 bytes, G2) + π_c (64 bytes, G1)
        /// Content: deterministic commitment derived from public inputs.
        /// When Zisk produces real proofs, only these bytes change — nothing else in the pipeline moves.
        /// </summary>
        public static byte[] BuildGroth16MockProof(byte[] preStateRoot, byte[] postStateRoot,
            long blockNumber, byte[] witnessHash)
        {
            var keccak = new Sha3Keccack();

            // Build the same binding the real prover would: commitment to public inputs
            // This mirrors §10 public inputs [5]=preStateRoot, [6]=postStateRoot
            var inputData = new byte[32 + 32 + 8 + 32];
            if (preStateRoot != null)
                Array.Copy(preStateRoot, 0, inputData, 0, Math.Min(preStateRoot.Length, 32));
            if (postStateRoot != null)
                Array.Copy(postStateRoot, 0, inputData, 32, Math.Min(postStateRoot.Length, 32));
            BitConverter.GetBytes(blockNumber).CopyTo(inputData, 64);
            if (witnessHash != null)
                Array.Copy(witnessHash, 0, inputData, 72, Math.Min(witnessHash.Length, 32));

            var commitment = keccak.CalculateHash(inputData);

            // Fill 256 bytes: π_a = commitment padded, π_b = double-hash, π_c = triple-hash
            var proof = new byte[Groth16ProofSize];
            // π_a (64 bytes) — the primary commitment
            Array.Copy(commitment, 0, proof, 0, 32);
            Array.Copy(commitment, 0, proof, 32, 32);
            // π_b (128 bytes) — derived
            var piB = keccak.CalculateHash(commitment);
            Array.Copy(piB, 0, proof, 64, 32);
            Array.Copy(piB, 0, proof, 96, 32);
            Array.Copy(piB, 0, proof, 128, 32);
            Array.Copy(piB, 0, proof, 160, 32);
            // π_c (64 bytes) — derived
            var piC = keccak.CalculateHash(piB);
            Array.Copy(piC, 0, proof, 192, 32);
            Array.Copy(piC, 0, proof, 224, 32);

            return proof;
        }

        /// <summary>
        /// Verifies a mock Groth16 proof by reconstructing the commitment from public inputs.
        /// This is what MockProofVerifier.sol does on-chain.
        /// </summary>
        public static bool VerifyMockProof(byte[] proof, byte[] preStateRoot, byte[] postStateRoot,
            long blockNumber, byte[] witnessHash)
        {
            if (proof == null || proof.Length != Groth16ProofSize) return false;

            var expected = BuildGroth16MockProof(preStateRoot, postStateRoot, blockNumber, witnessHash);
            return ByteUtil.AreEqual(proof, expected);
        }
    }
}
