using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Merkle.Patricia
{

    public static class AccountProofVerification
    {

        public static bool VerifyAccountProofs(string accountAddress, byte[] stateRoot, IEnumerable<byte[]> rlpValueProofs, Account account)
        {
            var encoded = new AccountEncoder();
            var accountEncoded = encoded.Encode(account);

            var trie = new PatriciaTrie(stateRoot);
            var sha3Provider = new Sha3KeccackHashProvider();
            var inMemoryStorage = new InMemoryTrieStorage();

            foreach (var proofItem in rlpValueProofs)
            {
                inMemoryStorage.Put(sha3Provider.ComputeHash(proofItem), proofItem);
            }

            var value = trie.Get(sha3Provider.ComputeHash(accountAddress.HexToByteArray()), inMemoryStorage);
            if (trie.Root.GetHash().AreTheSame(stateRoot))
            {
                if (accountEncoded.AreTheSame(value)) return true;
                return false;
            }
            return false;
        }
    }
}
