using System.Collections.Generic;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Model;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Patricia.ProofVerification
{
    public static class PatriciaProofVerifier
    {
        public static bool TryVerify(byte[] rootHash, byte[] key, IList<byte[]> proof, out byte[] value)
        {
            value = null;
            if (rootHash == null || key == null) return false;
            if (rootHash.AreTheSame(DefaultValues.EMPTY_TRIE_HASH)) return true;
            if (proof == null || proof.Count == 0) return false;

            var proofDb = PatriciaRangeProofVerifier.BuildProofStorage(proof, Sha3KeccackHashProvider.Instance);
            try
            {
                PatriciaRangeProofVerifier.ProofToPath(
                    rootHash, null, PatriciaRangeProofVerifier.KeyBytesToHex(key), proofDb, allowNonExistent: true, out value);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }
    }
}
