using Nethereum.Model;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// One transaction passed into <see cref="BlockExecutor.ExecuteAsync"/>
    /// with an optional pre-recovered sender address. The sequencer's
    /// ordering policy recovers senders up front (one ECDSA per tx) and
    /// caches them in <see cref="CachedSender"/> so the engine doesn't
    /// re-recover during execution; followers usually pass
    /// <c>CachedSender = null</c> and let
    /// <see cref="TransactionProcessor.ExecuteTransactionAsync"/> recover
    /// lazily.
    /// </summary>
    public readonly struct TxEntry
    {
        public TxEntry(ISignedTransaction tx, string? cachedSender = null)
        {
            Tx = tx;
            CachedSender = cachedSender;
        }

        public ISignedTransaction Tx { get; }
        public string? CachedSender { get; }
    }
}
