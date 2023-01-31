using Nethereum.Util.HashProviders;
using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia
{
    public static class StorageProofVerification
    {
        public static bool ValidateValueFromStorageProof(byte[] key, byte[] value, IEnumerable<byte[]> proofs, byte[] stateRoot)
        {
            var trie = new PatriciaTrie(stateRoot);

            var sha3Provider = new Sha3KeccackHashProvider();
            var inMemoryStorage = new InMemoryTrieStorage();

            foreach (var proofItem in proofs)
            {
                inMemoryStorage.Put(sha3Provider.ComputeHash(proofItem), proofItem);
            }

            var valueFromTrie = trie.Get(key, inMemoryStorage);

            if (valueFromTrie.AreTheSame(value))
            {
                if (trie.Root.GetHash().AreTheSame(stateRoot))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
