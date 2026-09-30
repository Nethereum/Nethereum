namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class Eip7594MaxBlobsPerTxRule : ITransactionValidationRule
    {
        public const int MAX_BLOBS_PER_TX = 6;

        public static readonly Eip7594MaxBlobsPerTxRule Instance = new Eip7594MaxBlobsPerTxRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (!ctx.IsType3Transaction) return;
            if (ctx.BlobVersionedHashes == null) return;
            if (ctx.BlobVersionedHashes.Count > MAX_BLOBS_PER_TX)
                throw new TransactionValidationException(TransactionError.Type3TxBlobCountExceeded, "TYPE_3_TX_BLOB_COUNT_EXCEEDED");
        }
    }
}
