using Nethereum.Util.HashProviders;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;

namespace Nethereum.Merkle.Patricia
{
    public static class StorageProofVerification
    {
        public static bool ValidateValueFromStorageProof(byte[] key, byte[] value, IEnumerable<byte[]> proofs, byte[] stateRoot = null)
        {
            var sha3Provider = new Sha3KeccackHashProvider();

            if (stateRoot == null) //should be the same
            {
                stateRoot = sha3Provider.ComputeHash(proofs.First());
            }

            var trie = new PatriciaTrie(stateRoot);

          
            var inMemoryStorage = new InMemoryTrieStorage();

            foreach (var proofItem in proofs)
            {
                inMemoryStorage.Put(sha3Provider.ComputeHash(proofItem), proofItem);
            }

            var valueFromTrie = trie.Get(key, inMemoryStorage);

            if(valueFromTrie == null)
            {
                //TODO (Do we want to do this?)
                //trie.Put(key, value, inMemoryStorage); // setting the value and verifying the hash roots.
                //valueFromTrie = value;

                if (trie.Root.GetHash().AreTheSame(stateRoot))
                {
                    return true;
                }
            }

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
