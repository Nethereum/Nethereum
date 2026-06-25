using System;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Fork-aware tx-type acceptance gate. Mirrors the per-fork
    /// <see cref="ITransactionDecoder"/> rejection logic but operates on
    /// an already-decoded <see cref="ISignedTransaction"/> — useful for
    /// validating peer-supplied bodies where the decode has already
    /// happened a layer below (e.g., <c>BlockBodiesMessage.Decode</c>
    /// went through global <c>TransactionFactory.CreateTransaction</c>
    /// and we now want to reject the batch if any tx type is not yet
    /// active at the block's fork).
    ///
    /// <para>A malicious peer that returns a Type-4 (EIP-7702) tx as
    /// part of a Frontier-era block body must be rejected, otherwise
    /// downstream execution would silently accept it. The canonical guard
    /// rejects the block when the tx type exceeds the chain's active set.</para>
    /// </summary>
    public static class TransactionTypeFork
    {
        public static bool IsAcceptedAt(ISignedTransaction tx, HardforkName fork)
        {
            if (tx is null) throw new ArgumentNullException(nameof(tx));

            // EIP-7702 (Prague+) — set-code tx (0x04).
            if (tx is Transaction7702)
                return fork >= HardforkName.Prague;

            // EIP-4844 (Cancun+) — blob tx (0x03).
            if (tx is Transaction4844)
                return fork >= HardforkName.Cancun;

            // EIP-1559 (London+) — dynamic-fee tx (0x02).
            if (tx is Transaction1559)
                return fork >= HardforkName.London;

            // EIP-2930 (Berlin+) — access-list tx (0x01).
            if (tx is Transaction2930)
                return fork >= HardforkName.Berlin;

            // Legacy + EIP-155 — accepted everywhere.
            if (tx is LegacyTransaction || tx is LegacyTransactionChainId)
                return true;

            // Unknown subtype — fail closed.
            return false;
        }
    }
}
