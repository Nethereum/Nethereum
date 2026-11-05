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
        public const int DefaultMaxTransactionSizeBytes = TransactionAdmissionRules.DefaultMaxTransactionSizeBytes;

        public const int DefaultMaxBlobTransactionSizeBytes = TransactionAdmissionRules.DefaultMaxBlobTransactionSizeBytes;

        public const long DefaultMaxGasLimit = 100_000_000;

        private readonly TransactionAdmissionRules _rules;
        private readonly IntrinsicGasRules _intrinsicGas;

        public MempoolAdmissionValidator(
            System.Numerics.BigInteger chainId,
            IntrinsicGasRules intrinsicGas = null,
            int maxTransactionSizeBytes = DefaultMaxTransactionSizeBytes,
            long maxGasLimit = DefaultMaxGasLimit,
            int maxBlobTransactionSizeBytes = DefaultMaxBlobTransactionSizeBytes)
        {
            _rules = new TransactionAdmissionRules(chainId, maxGasLimit, maxTransactionSizeBytes, maxBlobTransactionSizeBytes);
            _intrinsicGas = intrinsicGas ?? IntrinsicGasRuleSets.Prague;
        }

        public async Task<MempoolAdmission> ValidateAsync(
            ISignedTransaction tx,
            IChainStoreBundle bundle,
            ITxPool txPool,
            CancellationToken cancellationToken = default)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            if (tx is Transaction4844 shapeBlobTx && !BlobSidecarValidator.HasSidecar(shapeBlobTx))
                return MempoolAdmission.Reject(MempoolRejectReason.BlobSidecarMissing,
                    "type-3 blob transaction has no sidecar to relay");

            if (_rules.BlobCountReason(tx) is string blobCountInvalid)
                return MempoolAdmission.Reject(MempoolRejectReason.BlobCountInvalid, blobCountInvalid);

            if (_rules.OversizeReason(tx) is string oversize)
                return MempoolAdmission.Reject(MempoolRejectReason.Oversize, oversize);

            if (_rules.InvalidSignatureReason(tx) is string invalidSignature)
                return MempoolAdmission.Reject(MempoolRejectReason.InvalidSignature, invalidSignature);

            var sender = tx.GetSenderAddress();
            if (string.IsNullOrEmpty(sender))
                return MempoolAdmission.Reject(MempoolRejectReason.InvalidSignature,
                    "no sender could be recovered");
            var senderKey = sender.ToLowerInvariant();

            if (_rules.WrongChainIdReason(tx) is string wrongChainId)
                return MempoolAdmission.Reject(MempoolRejectReason.WrongChainId, wrongChainId, senderKey);

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

            if (_rules.GasLimitTooHighReason(tx) is string gasLimitTooHigh)
                return MempoolAdmission.Reject(MempoolRejectReason.GasLimitTooHigh, gasLimitTooHigh, senderKey);

            if (_rules.FeeCapBelowPriorityFeeReason(tx) is string feeCapBelowPriorityFee)
                return MempoolAdmission.Reject(MempoolRejectReason.FeeCapBelowPriorityFee, feeCapBelowPriorityFee, senderKey);

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
