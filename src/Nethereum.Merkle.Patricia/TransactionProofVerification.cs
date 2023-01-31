using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia
{
    public static class TransactionProofVerification
    {
        public static bool ValidateTransactions(string transactionsRoot, List<IndexedSignedTransaction> transactions)
        {
            var trie = new PatriciaTrie();

            foreach (var transaction in transactions)
            {
                trie.Put(RLP.RLP.EncodeElement(transaction.Index.ToBytesForRLPEncoding()), transaction.SignedTransaction.GetRLPEncoded());
            }
            var valid = trie.Root.GetHash().AreTheSame(transactionsRoot.HexToByteArray());
            return valid;
        }
    }
}
