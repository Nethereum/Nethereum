using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using System.Collections.Generic;

using Nethereum.Merkle.Patricia.Nodes;
namespace Nethereum.Merkle.Patricia.ProofVerification
{
    public interface ITransactionProofVerifier
    {
        bool Verify(string transactionsRoot, List<IndexedSignedTransaction> transactions);
    }

    public class TransactionProofVerification : ITransactionProofVerifier
    {
        public static TransactionProofVerification Current { get; } = new TransactionProofVerification();

        public bool Verify(string transactionsRoot, List<IndexedSignedTransaction> transactions)
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
