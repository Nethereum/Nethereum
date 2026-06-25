using Nethereum.Util.HashProviders;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Util;
using Nethereum.Model;

namespace Nethereum.Merkle.Patricia
{
    public static class StorageProofVerification
    {
        /// <summary>
        /// Verify a single storage-slot value against an <c>eth_getProof</c>-style proof and the
        /// account's storage root.
        ///
        /// Two cases:
        ///   - Inclusion: the trie rooted at <paramref name="stateRoot"/> contains a value at the
        ///     hashed key, and that value equals the RLP-encoded claimed value.
        ///   - Non-inclusion: the trie rooted at <paramref name="stateRoot"/> does NOT contain the
        ///     key. By spec the claimed value must then be zero (the default for an uninitialised
        ///     storage slot). Clients return value=0x0 with the proof path that proves absence.
        /// </summary>
        public static bool ValidateValueFromStorageProof(byte[] key, byte[] value, IList<byte[]> proofs, byte[] stateRoot = null)
        {
            var sha3Provider = new Sha3KeccackHashProvider();

            if (stateRoot == null)
            {
                stateRoot = sha3Provider.ComputeHash(proofs[0]);
            }

            var trie = new PatriciaTrie(stateRoot);

            var inMemoryStorage = new InMemoryTrieStorage();
            foreach (var proofItem in proofs)
            {
                inMemoryStorage.Put(sha3Provider.ComputeHash(proofItem), proofItem);
            }

            var keyEncoded = AccountStorage.EncodeKeyForStorage(key, sha3Provider);
            var valueEncoded = AccountStorage.EncodeValueForStorage(value);

            byte[] valueFromTrie = trie.Get(keyEncoded, inMemoryStorage);

            if (valueFromTrie == null)
            {
                // Non-inclusion proof: the trie traversal from stateRoot reached the position
                // where the key would live but found nothing. Valid iff the claimed value is the
                // storage default (zero / empty).
                return value == null || value.Length == 0 || value.All(b => b == 0);
            }

            // Inclusion proof: the trie at stateRoot did surface a value for our key.
            // The trie is constructed with Root = stateRoot, so root equality is implicit —
            // we only need to confirm the value matches what the caller claimed.
            return valueFromTrie.AreTheSame(valueEncoded);
        }
    }
}
