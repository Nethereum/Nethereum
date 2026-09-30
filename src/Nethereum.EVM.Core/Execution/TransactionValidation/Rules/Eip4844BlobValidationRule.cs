using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    /// <summary>
    /// EIP-4844: "there must be at least one blob" — <c>assert len(tx.blob_versioned_hashes) &gt; 0</c>;
    /// "all versioned blob hashes must start with VERSIONED_HASH_VERSION_KZG" —
    /// <c>assert h[0] == VERSIONED_HASH_VERSION_KZG</c>; and "The field <c>to</c> deviates slightly
    /// from the semantics with the exception that it MUST NOT be <c>nil</c> and therefore must
    /// always represent a 20-byte address. This means that blob transactions cannot have the form
    /// of a create transaction."
    ///
    /// <para>The order below is OBSERVABLE and must not be rearranged: a type-3 that is both
    /// zero-blob and a contract creation satisfies two rejection conditions, and a fixture asserts
    /// which one fired. The EIP's own validity block asserts the two blob conditions and does not
    /// mention <c>to</c>, whose constraint is checked after them (checked against EELS
    /// amsterdam/transactions.py:632-644, then :646-648).</para>
    /// </summary>
    public sealed class Eip4844BlobValidationRule : ITransactionValidationRule
    {
        private const byte VERSIONED_HASH_VERSION_KZG = 0x01;

        public static readonly Eip4844BlobValidationRule Instance = new Eip4844BlobValidationRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (!ctx.IsType3Transaction)
                return;

            RejectZeroBlobs(ctx);
            RejectInvalidVersionedHashes(ctx);
            RejectContractCreation(ctx);
        }

        private static void RejectZeroBlobs(TransactionExecutionContext ctx)
        {
            if (ctx.BlobVersionedHashes == null || ctx.BlobVersionedHashes.Count == 0)
                throw new TransactionValidationException(TransactionError.Type3TxZeroBlobs, "TYPE_3_TX_ZERO_BLOBS");
        }

        private static void RejectInvalidVersionedHashes(TransactionExecutionContext ctx)
        {
            foreach (var hash in ctx.BlobVersionedHashes)
            {
                var hashBytes = hash.HexToByteArray();
                if (hashBytes.Length < 1 || hashBytes[0] != VERSIONED_HASH_VERSION_KZG)
                    throw new TransactionValidationException(TransactionError.Type3TxInvalidBlobVersionedHash, "TYPE_3_TX_INVALID_BLOB_VERSIONED_HASH");
            }
        }

        private static void RejectContractCreation(TransactionExecutionContext ctx)
        {
            if (ctx.IsContractCreation)
                throw new TransactionValidationException(TransactionError.Type3TxContractCreation, "TYPE_3_TX_CONTRACT_CREATION");
        }
    }
}
