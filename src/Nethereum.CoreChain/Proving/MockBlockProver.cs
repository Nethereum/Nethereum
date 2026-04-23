using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Proving
{
    public class MockBlockProver : IBlockProver
    {
        public Task<BlockProofResult> ProveBlockAsync(byte[] witnessBytes,
            byte[] preStateRoot, byte[] postStateRoot, long blockNumber)
        {
            byte[] proofBytes;
            byte[] witnessHash;
            using (var sha = SHA256.Create())
            {
                witnessHash = sha.ComputeHash(witnessBytes);
                var combined = new byte[witnessHash.Length + (preStateRoot?.Length ?? 0) + (postStateRoot?.Length ?? 0)];
                int offset = 0;
                if (preStateRoot != null) { System.Array.Copy(preStateRoot, 0, combined, offset, preStateRoot.Length); offset += preStateRoot.Length; }
                if (postStateRoot != null) { System.Array.Copy(postStateRoot, 0, combined, offset, postStateRoot.Length); offset += postStateRoot.Length; }
                System.Array.Copy(witnessHash, 0, combined, offset, witnessHash.Length);
                proofBytes = sha.ComputeHash(combined);
            }

            return Task.FromResult(new BlockProofResult
            {
                ProofBytes = proofBytes,
                PreStateRoot = preStateRoot,
                PostStateRoot = postStateRoot,
                BlockNumber = blockNumber,
                WitnessHash = witnessHash,
                ProverMode = "Mock"
            });
        }
    }
}
