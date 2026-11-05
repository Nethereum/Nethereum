using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.CoreChain
{
    internal sealed class SenderRecoveryStep
    {
        public SenderRecoveryStep(ITransactionVerificationAndRecovery? verifier)
        {
            Verifier = verifier ?? new TransactionVerificationAndRecoveryImp();
        }

        public ITransactionVerificationAndRecovery Verifier { get; }

        public RecoveredSenders Begin(ISignedTransaction?[] transactions)
        {
            var senders = new RecoveredSenders(Verifier, transactions);
            SenderRecoveryWorkers.Submit(senders);
            return senders;
        }
    }
}
