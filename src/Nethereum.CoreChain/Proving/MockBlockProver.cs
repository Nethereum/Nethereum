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

        public static byte[] BuildGroth16MockProof(byte[] preStateRoot, byte[] postStateRoot,
            long blockNumber, byte[] witnessHash)
        {
            var keccak = new Sha3Keccack();

            var inputData = new byte[32 + 32 + 8 + 32];
            if (preStateRoot != null)
                Array.Copy(preStateRoot, 0, inputData, 0, Math.Min(preStateRoot.Length, 32));
            if (postStateRoot != null)
                Array.Copy(postStateRoot, 0, inputData, 32, Math.Min(postStateRoot.Length, 32));
            BitConverter.GetBytes(blockNumber).CopyTo(inputData, 64);
            if (witnessHash != null)
                Array.Copy(witnessHash, 0, inputData, 72, Math.Min(witnessHash.Length, 32));

            var commitment = keccak.CalculateHash(inputData);

            var proof = new byte[Groth16ProofSize];
            Array.Copy(commitment, 0, proof, 0, 32);
            Array.Copy(commitment, 0, proof, 32, 32);
            var piB = keccak.CalculateHash(commitment);
            Array.Copy(piB, 0, proof, 64, 32);
            Array.Copy(piB, 0, proof, 96, 32);
            Array.Copy(piB, 0, proof, 128, 32);
            Array.Copy(piB, 0, proof, 160, 32);
            var piC = keccak.CalculateHash(piB);
            Array.Copy(piC, 0, proof, 192, 32);
            Array.Copy(piC, 0, proof, 224, 32);

            return proof;
        }

        public static bool VerifyMockProof(byte[] proof, byte[] preStateRoot, byte[] postStateRoot,
            long blockNumber, byte[] witnessHash)
        {
            if (proof == null || proof.Length != Groth16ProofSize) return false;

            var expected = BuildGroth16MockProof(preStateRoot, postStateRoot, blockNumber, witnessHash);
            return ByteUtil.AreEqual(proof, expected);
        }
    }
}
