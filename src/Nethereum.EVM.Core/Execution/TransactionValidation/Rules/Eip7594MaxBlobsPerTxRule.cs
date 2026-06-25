namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    /// <summary>
    /// EIP-7594 (PeerDAS) per-transaction blob cap. From Osaka onwards a
    /// single type-3 transaction may carry at most <see cref="MAX_BLOBS_PER_TX"/>
    /// blobs, decoupled from the per-block <c>MaxBlobsPerBlock</c> limit
    /// (which stays at 9 from EIP-7691).
    ///
    /// <para>
    /// Pre-Osaka forks rely on <see cref="Eip4844BlobValidationRule"/>'s
    /// <c>Count &gt; MaxBlobsPerBlock</c> check, which coincidentally
    /// enforces the per-tx cap because the two limits are equal at Cancun
    /// (6/6) and Prague (9/9). At Osaka EIP-7594 introduces the asymmetry
    /// — 6 per tx, 9 per block — so this rule is registered only in
    /// <c>TransactionValidationRuleSets.Osaka</c> onwards.
    /// </para>
    ///
    /// <para>
    /// Spec: <a href="https://eips.ethereum.org/EIPS/eip-7594">EIP-7594</a>.
    /// </para>
    /// </summary>
    public sealed class Eip7594MaxBlobsPerTxRule : ITransactionValidationRule
    {
        public const int MAX_BLOBS_PER_TX = 6;

        public static readonly Eip7594MaxBlobsPerTxRule Instance = new Eip7594MaxBlobsPerTxRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (!ctx.IsType3Transaction) return;
            if (ctx.BlobVersionedHashes == null) return;
            if (ctx.BlobVersionedHashes.Count > MAX_BLOBS_PER_TX)
                throw new TransactionValidationException("TYPE_3_TX_BLOB_COUNT_EXCEEDED");
        }
    }
}
