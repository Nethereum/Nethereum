
using Nethereum.Documentation;

namespace Nethereum.Merkle.Patricia.ProofVerification
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "state-proofs", "The front door onto every Patricia proof verifier")]
    public interface IProofVerification
    {
        IAccountProofVerifier Account { get; }
        IStorageProofVerifier Storage { get; }
        IRangeProofVerifier Range { get; }
        ITransactionProofVerifier Transaction { get; }
        ITrieNodeVerifier TrieNode { get; }
    }

    public class ProofVerification : IProofVerification
    {
        public static ProofVerification Current { get; } = new ProofVerification();

        public IAccountProofVerifier Account { get; }
        public IStorageProofVerifier Storage { get; }
        public IRangeProofVerifier Range { get; }
        public ITransactionProofVerifier Transaction { get; }
        public ITrieNodeVerifier TrieNode { get; }

        public ProofVerification()
            : this(
                AccountProofVerification.Current,
                StorageProofVerification.Current,
                PatriciaRangeProofVerifier.Current,
                TransactionProofVerification.Current,
                TrieNodeVerification.Current)
        {
        }

        public ProofVerification(
            IAccountProofVerifier account,
            IStorageProofVerifier storage,
            IRangeProofVerifier range,
            ITransactionProofVerifier transaction,
            ITrieNodeVerifier trieNode)
        {
            Account = account;
            Storage = storage;
            Range = range;
            Transaction = transaction;
            TrieNode = trieNode;
        }
    }
}
