using Nethereum.Util.HashProviders;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;
using System.Xml.Linq;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Model;

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

            var keyEncoded = AccountStorage.EncodeKeyForStorage(key, sha3Provider);
            var valueEncoded = AccountStorage.EncodeValueForStorage(value);


            byte[] valueFromTrie = trie.Get(keyEncoded, inMemoryStorage);

            if (valueFromTrie == null)
            {
                trie.Put(keyEncoded, valueEncoded, inMemoryStorage); // setting the value and verifying the hash roots.
               
                if (trie.Root.GetHash().AreTheSame(stateRoot))
                {
                    return true;
                }
            }

            if (valueFromTrie.AreTheSame(valueEncoded))
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
