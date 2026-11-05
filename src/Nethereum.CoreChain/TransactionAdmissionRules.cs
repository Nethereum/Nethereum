using Nethereum.Documentation;
using System;
using Nethereum.EVM.Execution.TransactionValidation.Rules;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "TransactionAdmissionRules - the one admission rule RPC and peer paths share")]
    public sealed class TransactionAdmissionRules
    {
        public const int DefaultMaxTransactionSizeBytes = 128 * 1024;

        public const int DefaultMaxBlobTransactionSizeBytes = 1024 * 1024;

        private readonly EvmUInt256 _chainId;
        private readonly int _maxTransactionSizeBytes;
        private readonly int _maxBlobTransactionSizeBytes;
        private readonly EvmUInt256 _maxGasLimit;

        public TransactionAdmissionRules(
            System.Numerics.BigInteger chainId,
            long maxGasLimit,
            int maxTransactionSizeBytes = DefaultMaxTransactionSizeBytes,
            int maxBlobTransactionSizeBytes = DefaultMaxBlobTransactionSizeBytes)
        {
            _chainId = (EvmUInt256)chainId;
            _maxGasLimit = new EvmUInt256((ulong)maxGasLimit);
            _maxTransactionSizeBytes = maxTransactionSizeBytes;
            _maxBlobTransactionSizeBytes = maxBlobTransactionSizeBytes;
        }

        public string? FirstRefusal(ISignedTransaction tx) =>
            BlobCountReason(tx)
            ?? OversizeReason(tx)
            ?? InvalidSignatureReason(tx)
            ?? WrongChainIdReason(tx)
            ?? GasLimitTooHighReason(tx)
            ?? FeeCapBelowPriorityFeeReason(tx);

        public string? BlobCountReason(ISignedTransaction tx)
        {
            if (tx is not Transaction4844 blobTx) return null;
            var blobCount = blobTx.BlobVersionedHashes?.Count ?? 0;
            return blobCount < 1 || blobCount > Eip7594MaxBlobsPerTxRule.MAX_BLOBS_PER_TX
                ? $"blob count {blobCount} not in [1, {Eip7594MaxBlobsPerTxRule.MAX_BLOBS_PER_TX}]"
                : null;
        }

        public string? OversizeReason(ISignedTransaction tx)
        {
            if (tx is Transaction4844 blobTx)
            {
                var blobSize = (blobTx.Sidecar != null ? blobTx.GetRLPEncodedWithSidecar() : blobTx.GetRLPEncoded()).Length;
                return blobSize > _maxBlobTransactionSizeBytes
                    ? $"blob transaction size {blobSize} exceeds blob cap {_maxBlobTransactionSizeBytes}"
                    : null;
            }

            var size = tx.GetRLPEncoded().Length;
            return size > _maxTransactionSizeBytes
                ? $"transaction size {size} exceeds cap {_maxTransactionSizeBytes}"
                : null;
        }

        public string? InvalidSignatureReason(ISignedTransaction tx) =>
            tx.VerifyTransaction() ? null : "signature failed to verify";

        public string? WrongChainIdReason(ISignedTransaction tx) =>
            tx.DeclaresItsOwnChainId() && tx.GetChainId() != _chainId
                ? $"transaction chain id {tx.GetChainId()} does not match node chain id {_chainId}"
                : null;

        public string? GasLimitTooHighReason(ISignedTransaction tx)
        {
            var gasLimit = tx.GetGasLimit();
            return gasLimit > _maxGasLimit
                ? $"gas limit {gasLimit} exceeds cap {_maxGasLimit}"
                : null;
        }

        public string? FeeCapBelowPriorityFeeReason(ISignedTransaction tx) =>
            tx.IsFeeMarketCapable() && tx.GetMaxPriorityFeePerGas() > tx.GetMaxFeePerGas()
                ? $"maxPriorityFeePerGas {tx.GetMaxPriorityFeePerGas()} exceeds maxFeePerGas {tx.GetMaxFeePerGas()}"
                : null;
    }
}
