using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using System.Collections.Generic;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.ProofVerification
{
    public interface IAccountProofVerifier
    {
        bool Verify(byte[] stateRoot, IEnumerable<byte[]> proof, string accountAddress, Account account);
    }

    public class AccountProofVerification : IAccountProofVerifier
    {
        public static AccountProofVerification Current { get; } = new AccountProofVerification();

        public bool Verify(byte[] stateRoot, IEnumerable<byte[]> proof, string accountAddress, Account account)
        {
            var encoded = new AccountEncoder();
            var accountEncoded = encoded.Encode(account);

            var sha3Provider = Sha3KeccackHashProvider.Instance;
            var inMemoryStorage = new InMemoryContentNodeStore();

            foreach (var proofItem in proof)
            {
                inMemoryStorage.Put(sha3Provider.ComputeHash(proofItem), proofItem);
            }

            var trie = new PatriciaTrie(stateRoot, inMemoryStorage);
            var value = trie.Get(sha3Provider.ComputeHash(accountAddress.HexToByteArray()));
            if (trie.Root.GetHash().AreTheSame(stateRoot))
            {
                if (accountEncoded.AreTheSame(value)) return true;
                return false;
            }
            return false;
        }
    }
}
