using Nethereum.Util.HashProviders;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Util;
using Nethereum.Model;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.ProofVerification
{
    public interface IStorageProofVerifier
    {
        bool Verify(byte[] stateRoot, IList<byte[]> proof, byte[] key, byte[] value);
    }

    public class StorageProofVerification : IStorageProofVerifier
    {
        public static StorageProofVerification Current { get; } = new StorageProofVerification();

        public bool Verify(byte[] stateRoot, IList<byte[]> proof, byte[] key, byte[] value)
        {
            var sha3Provider = Sha3KeccackHashProvider.Instance;

            if (stateRoot == null)
            {
                stateRoot = sha3Provider.ComputeHash(proof[0]);
            }

            var inMemoryStorage = new InMemoryContentNodeStore();
            foreach (var proofItem in proof)
            {
                inMemoryStorage.Put(sha3Provider.ComputeHash(proofItem), proofItem);
            }

            var trie = new PatriciaTrie(stateRoot, inMemoryStorage);

            var keyEncoded = AccountStorage.EncodeKeyForStorage(key, sha3Provider);
            var valueEncoded = AccountStorage.EncodeValueForStorage(value);

            byte[] valueFromTrie = trie.Get(keyEncoded);

            if (valueFromTrie == null)
            {
                return value == null || value.Length == 0 || value.All(b => b == 0);
            }

            return valueFromTrie.AreTheSame(valueEncoded);
        }
    }
}
