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
            if (stateRoot == null || key == null || key.Length > 32) return false;

            var sha3Provider = Sha3KeccackHashProvider.Instance;
            var keyEncoded = AccountStorage.EncodeKeyForStorage(key, sha3Provider);

            if (!PatriciaProofVerifier.TryVerify(stateRoot, keyEncoded, proof, out var valueFromTrie)) return false;

            if (valueFromTrie == null)
            {
                return value == null || value.Length == 0 || value.All(b => b == 0);
            }

            return valueFromTrie.AreTheSame(AccountStorage.EncodeValueForStorage(value));
        }
    }
}
