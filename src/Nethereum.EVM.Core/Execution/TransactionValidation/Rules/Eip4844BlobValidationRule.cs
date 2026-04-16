using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class Eip4844BlobValidationRule : ITransactionValidationRule
    {
        private const byte VERSIONED_HASH_VERSION_KZG = 0x01;

        public static readonly Eip4844BlobValidationRule Instance = new Eip4844BlobValidationRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (!ctx.IsType3Transaction)
                return;

            if (ctx.IsContractCreation)
                throw new TransactionValidationException("TYPE_3_TX_CONTRACT_CREATION");

            if (ctx.BlobVersionedHashes == null || ctx.BlobVersionedHashes.Count == 0)
                throw new TransactionValidationException("TYPE_3_TX_ZERO_BLOBS");

            if (ctx.BlobVersionedHashes.Count > config.MaxBlobsPerBlock)
                throw new TransactionValidationException("TYPE_3_TX_BLOB_COUNT_EXCEEDED");

            foreach (var hash in ctx.BlobVersionedHashes)
            {
                var hashBytes = hash.HexToByteArray();
                if (hashBytes.Length < 1 || hashBytes[0] != VERSIONED_HASH_VERSION_KZG)
                    throw new TransactionValidationException("TYPE_3_TX_INVALID_BLOB_VERSIONED_HASH");
            }
        }
    }
}
