using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.Execution.TransactionValidation.Rules;
using Nethereum.EVM.Gas;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Mempool
{
    public sealed class MempoolAdmissionValidator
    {
        public const int DefaultMaxTransactionSizeBytes = 128 * 1024;

        public const int DefaultMaxBlobTransactionSizeBytes =
            Eip7594MaxBlobsPerTxRule.MAX_BLOBS_PER_TX * BlobGasCalculator.GAS_PER_BLOB
            + DefaultMaxTransactionSizeBytes;

        public const long DefaultMaxGasLimit = 100_000_000;

        private readonly EvmUInt256 _chainId;
        private readonly IntrinsicGasRules _intrinsicGas;
        private readonly int _maxTransactionSizeBytes;
        private readonly int _maxBlobTransactionSizeBytes;
        private readonly EvmUInt256 _maxGasLimit;

        public MempoolAdmissionValidator(
            System.Numerics.BigInteger chainId,
            IntrinsicGasRules intrinsicGas = null,
            int maxTransactionSizeBytes = DefaultMaxTransactionSizeBytes,
            long maxGasLimit = DefaultMaxGasLimit,
            int maxBlobTransactionSizeBytes = DefaultMaxBlobTransactionSizeBytes)
        {
            _chainId = (EvmUInt256)chainId;
            _intrinsicGas = intrinsicGas ?? IntrinsicGasRuleSets.Prague;
            _maxTransactionSizeBytes = maxTransactionSizeBytes;
            _maxBlobTransactionSizeBytes = maxBlobTransactionSizeBytes;
            _maxGasLimit = new EvmUInt256((ulong)maxGasLimit);
        }

        public async Task<MempoolAdmission> ValidateAsync(
            ISignedTransaction tx,
            IChainStoreBundle bundle,
            ITxPool txPool,
            CancellationToken cancellationToken = default)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            if (tx is Transaction4844 shapeBlobTx)
            {
                if (!BlobSidecarValidator.HasSidecar(shapeBlobTx))
                    return MempoolAdmission.Reject(MempoolRejectReason.BlobSidecarMissing,
                        "type-3 blob transaction has no sidecar to relay");

                var blobCount = shapeBlobTx.BlobVersionedHashes?.Count ?? 0;
                if (blobCount < 1 || blobCount > Eip7594MaxBlobsPerTxRule.MAX_BLOBS_PER_TX)
                    return MempoolAdmission.Reject(MempoolRejectReason.BlobCountInvalid,
                        $"blob count {blobCount} not in [1, {Eip7594MaxBlobsPerTxRule.MAX_BLOBS_PER_TX}]");
            }

            if (tx is Transaction4844 sizeBlobTx)
            {
                var blobEncoded = sizeBlobTx.GetRLPEncodedWithSidecar();
                if (blobEncoded.Length > _maxBlobTransactionSizeBytes)
                    return MempoolAdmission.Reject(MempoolRejectReason.Oversize,
                        $"blob transaction size {blobEncoded.Length} exceeds blob cap {_maxBlobTransactionSizeBytes}");
            }
            else
            {
                var encoded = tx.GetRLPEncoded();
                if (encoded.Length > _maxTransactionSizeBytes)
                    return MempoolAdmission.Reject(MempoolRejectReason.Oversize,
                        $"transaction size {encoded.Length} exceeds cap {_maxTransactionSizeBytes}");
            }

            if (!tx.VerifyTransaction())
                return MempoolAdmission.Reject(MempoolRejectReason.InvalidSignature,
                    "signature failed to verify");

            var sender = tx.GetSenderAddress();
            if (string.IsNullOrEmpty(sender))
                return MempoolAdmission.Reject(MempoolRejectReason.InvalidSignature,
                    "no sender could be recovered");
            var senderKey = sender.ToLowerInvariant();

            if (tx.DeclaresItsOwnChainId() && tx.GetChainId() != _chainId)
                return MempoolAdmission.Reject(MempoolRejectReason.WrongChainId,
                    $"transaction chain id {tx.GetChainId()} does not match node chain id {_chainId}",
                    senderKey);

            var data = tx.GetData() ?? Array.Empty<byte>();
            var isContractCreation = tx.IsContractCreation();
            var isSelfTransfer = !isContractCreation && sender.IsTheSameAddress(tx.GetReceiverAddress());
            var value = tx.GetValue();
            var hasValue = !value.IsZero;
            var accessList = AccessListEntry.From(tx.GetAccessList());

            var minimumGas = _intrinsicGas.CalculateMinimumGasLimit(
                data, isContractCreation, accessList, isSelfTransfer, hasValue);
            var gasLimit = tx.GetGasLimit();
            if (gasLimit < new EvmUInt256((ulong)minimumGas))
                return MempoolAdmission.Reject(MempoolRejectReason.IntrinsicGasTooLow,
                    $"gas limit {gasLimit} below intrinsic minimum {minimumGas}", senderKey);

            if (gasLimit > _maxGasLimit)
                return MempoolAdmission.Reject(MempoolRejectReason.GasLimitTooHigh,
                    $"gas limit {gasLimit} exceeds cap {_maxGasLimit}", senderKey);

            if (tx.IsFeeMarketCapable() && tx.GetMaxPriorityFeePerGas() > tx.GetMaxFeePerGas())
                return MempoolAdmission.Reject(MempoolRejectReason.FeeCapBelowPriorityFee,
                    $"maxPriorityFeePerGas {tx.GetMaxPriorityFeePerGas()} exceeds maxFeePerGas {tx.GetMaxFeePerGas()}",
                    senderKey);

            if (tx is Transaction4844 structureBlobTx
                && !BlobSidecarValidator.HasValidVersionedHashes(structureBlobTx))
                return MempoolAdmission.Reject(MempoolRejectReason.BlobVersionedHashInvalid,
                    "blob versioned hashes do not match sidecar commitments", senderKey);

            var account = await bundle.State.GetAccountAsync(senderKey).ConfigureAwait(false);
            var confirmedNonce = account?.Nonce ?? EvmUInt256.Zero;
            var balance = account?.Balance ?? EvmUInt256.Zero;

            var txNonce = tx.GetNonce();
            if (txNonce < confirmedNonce)
                return MempoolAdmission.Reject(MempoolRejectReason.NonceTooLow,
                    $"nonce {txNonce} below sender confirmed nonce {confirmedNonce}", senderKey);

            var maxFeePerGas = tx.GetMaxFeePerGas();
            var valueBig = (System.Numerics.BigInteger)value;
            var gasLimitBig = (System.Numerics.BigInteger)gasLimit;
            var maxFeeBig = (System.Numerics.BigInteger)maxFeePerGas;
            var balanceBig = (System.Numerics.BigInteger)balance;
            var cost = valueBig + gasLimitBig * maxFeeBig;

            if (tx is Transaction4844 costBlobTx)
            {
                var blobCount = costBlobTx.BlobVersionedHashes.Count;
                var maxFeePerBlobGas = (System.Numerics.BigInteger)costBlobTx.MaxFeePerBlobGas.GetValueOrDefault();
                cost += (System.Numerics.BigInteger)blobCount * BlobGasCalculator.GAS_PER_BLOB * maxFeePerBlobGas;
            }

            if (balanceBig < cost)
                return MempoolAdmission.Reject(MempoolRejectReason.InsufficientBalance,
                    $"balance {balanceBig} cannot cover value + gas*fee = {cost}", senderKey);

            return MempoolAdmission.Accept(senderKey);
        }
    }
}
