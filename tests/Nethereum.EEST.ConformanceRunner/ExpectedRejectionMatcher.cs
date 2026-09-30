#nullable enable
using System;
using Nethereum.Model;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.CoreChain;

namespace Nethereum.EEST.ConformanceRunner
{
    public readonly struct ObservedRejection
    {
        private ObservedRejection(
            TransactionError transactionReason,
            IReadOnlyList<BlockValidityCheck> failedChecks,
            Exception? fault)
        {
            TransactionReason = transactionReason != TransactionError.None
                ? transactionReason
                : (fault as TransactionValidationException)?.Reason ?? TransactionError.None;
            FailedChecks = failedChecks;
            Fault = fault;
        }

        public TransactionError TransactionReason { get; }

        public IReadOnlyList<BlockValidityCheck> FailedChecks { get; }

        public Exception? Fault { get; }

        public bool IsSilent =>
            TransactionReason == TransactionError.None && FailedChecks.Count == 0 && Fault == null;

        public static ObservedRejection FromImport(
            IReadOnlyList<BlockValidityCheck>? failedChecks,
            TransactionError transactionReason,
            Exception? fault) =>
            new ObservedRejection(
                transactionReason,
                failedChecks ?? Array.Empty<BlockValidityCheck>(),
                fault);

        public static ObservedRejection FromFault(Exception? fault) =>
            new ObservedRejection(TransactionError.None, Array.Empty<BlockValidityCheck>(), fault);

        public static ObservedRejection FromTransaction(TransactionError transactionReason) =>
            new ObservedRejection(transactionReason, Array.Empty<BlockValidityCheck>(), null);

        public override string ToString()
        {
            var parts = new List<string>();
            if (TransactionReason != TransactionError.None) parts.Add($"TransactionError.{TransactionReason}");
            if (FailedChecks.Count > 0) parts.Add($"failedChecks=[{string.Join(",", FailedChecks.Select(c => c.Identifier()))}]");
            if (Fault != null) parts.Add($"{Fault.GetType().Name}: {Fault.Message}");
            return parts.Count == 0 ? "no machine-readable rejection reason" : string.Join("; ", parts);
        }
    }

    public static class ExpectedRejectionMatcher
    {
        public static bool Satisfies(string expectException, in ObservedRejection observed, out string detail)
        {
            if (string.IsNullOrEmpty(expectException))
                throw new ArgumentException("A fixture with no expectException is not a rejection fixture.", nameof(expectException));

            if (!IsDeliberateRejection(observed, out detail))
                return false;

            var alternatives = expectException.Split('|');

            var unmapped = new List<string>();
            var unimplemented = new List<string>();

            foreach (var alternative in alternatives)
            {
                var label = BareLabel(alternative);
                if (!Table.TryGetValue(label, out var rule))
                {
                    unmapped.Add(alternative);
                    continue;
                }

                if (rule.Unimplemented != null)
                {
                    unimplemented.Add($"{alternative} — {rule.Unimplemented}");
                    continue;
                }

                if (rule.SatisfiedBy(observed))
                {
                    detail = $"{alternative} matched by {observed}";
                    return true;
                }
            }

            if (unmapped.Count > 0)
            {
                detail =
                    $"UNMAPPED expectException label(s) {string.Join(", ", unmapped)} — " +
                    $"add them to {nameof(ExpectedRejectionMatcher)}.{nameof(Table)}; observed {observed}";
                return false;
            }

            if (unimplemented.Count == alternatives.Length)
            {
                detail =
                    $"no rule implemented for {string.Join("; ", unimplemented)}; observed {observed}";
                return false;
            }

            detail = $"expected {expectException}, observed {observed}";
            return false;
        }

        public static bool Satisfies(string expectException, Exception? fault, out string detail) =>
            Satisfies(expectException, ObservedRejection.FromFault(fault), out detail);

        public static bool Satisfies(string expectException, TransactionError transactionReason, out string detail) =>
            Satisfies(expectException, ObservedRejection.FromTransaction(transactionReason), out detail);

        public enum EngineReasonVerdict { Matched, Mismatch, NotModeled }

        public static EngineReasonVerdict MatchEngineInvalidReason(
            string expectException, string description, out string detail)
        {
            if (string.IsNullOrEmpty(expectException))
                throw new ArgumentException("A fixture with no expectException is not a rejection fixture.", nameof(expectException));

            description ??= "";
            var alternatives = expectException.Split('|');
            var unresolved = new List<string>();

            foreach (var alternative in alternatives)
            {
                var label = BareLabel(alternative);

                if (label == "INVALID_BLOCK_HASH")
                {
                    if (description.Contains("passed every validity check"))
                    {
                        detail = $"{alternative} matched by engine block-hash check";
                        return EngineReasonVerdict.Matched;
                    }
                    unresolved.Add(alternative);
                    continue;
                }

                if (!Table.TryGetValue(label, out var rule) || rule.Unimplemented != null)
                {
                    unresolved.Add(alternative);
                    continue;
                }

                if (DescriptionEvidences(rule, description))
                {
                    detail = $"{alternative} matched by description \"{description}\"";
                    return EngineReasonVerdict.Matched;
                }
            }

            if (unresolved.Count == alternatives.Length)
            {
                detail = $"{expectException} is not modeled as a distinct engine verdict; description \"{description}\"";
                return EngineReasonVerdict.NotModeled;
            }

            detail = $"expected {expectException}, engine described \"{description}\"";
            return EngineReasonVerdict.Mismatch;
        }

        private static bool DescriptionEvidences(RejectionRule rule, string description)
        {
            if (rule.SystemCallFailure && description.Contains("SystemCallFailed")) return true;
            if (rule.FaultType != null && description.Contains(rule.FaultType.Name)) return true;

            foreach (var reason in rule.TransactionReasons)
                if (description.Contains(reason.ToString())) return true;

            foreach (var check in rule.BlockChecks)
                if (description.Contains(check.Identifier())) return true;

            return false;
        }

        public static bool SatisfiedByDecodeFailure(string expectException)
        {
            if (string.IsNullOrEmpty(expectException)) return false;
            foreach (var alternative in expectException.Split('|'))
            {
                switch (BareLabel(alternative))
                {
                    case "INCORRECT_BLOCK_FORMAT":
                    case "RLP_STRUCTURES_ENCODING":
                    case "RLP_WITHDRAWALS_NOT_READ":
                    case "RLP_INVALID_FIELD_OVERFLOW_64":
                        return true;
                }
            }
            return false;
        }

        private static bool IsDeliberateRejection(in ObservedRejection observed, out string detail)
        {
            var fault = observed.Fault;
            if (fault != null)
            {
                if (EvmHostException.IsHostOrSystemFault(fault))
                {
                    detail = $"host/system fault {fault.GetType().Name}: {fault.Message}";
                    return false;
                }

                if (!(fault is TransactionValidationException
                      || fault is SystemCallFailedException
                      || fault is ScalarWiderThanItsFieldException
                      || fault is NonCanonicalScalarRlpException
                      || fault is MalformedDepositLogException
                      || fault is SystemCallPredeployMissingException))
                {
                    detail = $"unexpected {fault.GetType().Name}: {fault.Message}";
                    return false;
                }
            }
            else if (observed.IsSilent)
            {
                detail = "the block was refused without naming a reason (no reason code, no failed check, no exception)";
                return false;
            }

            detail = observed.ToString();
            return true;
        }

        private static string BareLabel(string alternative)
        {
            var trimmed = alternative.Trim();
            var dot = trimmed.LastIndexOf('.');
            return dot < 0 ? trimmed : trimmed.Substring(dot + 1);
        }

        private sealed class RejectionRule
        {
            private RejectionRule(
                TransactionError[] transactionReasons,
                BlockValidityCheck[] blockChecks,
                bool systemCallFailure,
                string? unimplemented)
            {
                TransactionReasons = transactionReasons;
                BlockChecks = blockChecks;
                SystemCallFailure = systemCallFailure;
                Unimplemented = unimplemented;
            }

            public TransactionError[] TransactionReasons { get; }
            public BlockValidityCheck[] BlockChecks { get; }
            public bool SystemCallFailure { get; }

            public string? Unimplemented { get; }

            public Type? FaultType { get; private set; }

            public static RejectionRule Tx(params TransactionError[] reasons) =>
                new RejectionRule(reasons, Array.Empty<BlockValidityCheck>(), false, null);

            public static RejectionRule Block(params BlockValidityCheck[] checks) =>
                new RejectionRule(Array.Empty<TransactionError>(), checks, false, null);

            public static RejectionRule TxOrBlock(TransactionError[] reasons, BlockValidityCheck[] checks) =>
                new RejectionRule(reasons, checks, false, null);

            public static RejectionRule SystemCall() =>
                new RejectionRule(Array.Empty<TransactionError>(), Array.Empty<BlockValidityCheck>(), true, null);

            public static RejectionRule Fault<TFault>() where TFault : Exception =>
                new RejectionRule(Array.Empty<TransactionError>(), Array.Empty<BlockValidityCheck>(), false, null)
                {
                    FaultType = typeof(TFault)
                };

            public static RejectionRule NotImplemented(string why) =>
                new RejectionRule(Array.Empty<TransactionError>(), Array.Empty<BlockValidityCheck>(), false, why);

            public bool SatisfiedBy(in ObservedRejection observed)
            {
                if (SystemCallFailure && observed.Fault is SystemCallFailedException) return true;

                if (FaultType != null && observed.Fault != null && FaultType.IsInstanceOfType(observed.Fault)) return true;

                foreach (var reason in TransactionReasons)
                    if (observed.TransactionReason == reason) return true;

                foreach (var check in BlockChecks)
                    if (observed.FailedChecks.Contains(check)) return true;

                return false;
            }
        }

        private const string NoHeaderPrecheck =
            "neither runner validates the header against its parent before execution, so no rejection of this kind can occur";

        private const string NoRlpVerdict =
            "block RLP well-formedness is not a verdict either engine renders — the harness decodes the block before the engine sees it";

        private static string NoDimension(string what) =>
            $"no BlockValidityCheck models {what}, so a mismatch there cannot be named as the reason a block was refused";

        private static readonly IReadOnlyDictionary<string, RejectionRule> Table =
            new Dictionary<string, RejectionRule>(StringComparer.Ordinal)
            {
                ["INTRINSIC_GAS_TOO_LOW"] = RejectionRule.Tx(TransactionError.IntrinsicGasTooLow),
                ["INTRINSIC_GAS_BELOW_FLOOR_GAS_COST"] = RejectionRule.Tx(TransactionError.IntrinsicGasTooLow),
                ["GAS_LIMIT_EXCEEDS_MAXIMUM"] = RejectionRule.Tx(TransactionError.GasLimitExceedsMaximum),

                ["GAS_ALLOWANCE_EXCEEDED"] = RejectionRule.Tx(TransactionError.GasAllowanceExceeded),
                ["INITCODE_SIZE_EXCEEDED"] = RejectionRule.Tx(TransactionError.InitcodeSizeExceeded),
                ["INSUFFICIENT_ACCOUNT_FUNDS"] = RejectionRule.Tx(TransactionError.InsufficientBalance),
                ["INSUFFICIENT_MAX_FEE_PER_GAS"] = RejectionRule.Tx(TransactionError.InsufficientMaxFeePerGas),
                ["INSUFFICIENT_MAX_FEE_PER_BLOB_GAS"] = RejectionRule.Tx(TransactionError.InsufficientMaxFeePerBlobGas),
                ["PRIORITY_GREATER_THAN_MAX_FEE_PER_GAS"] = RejectionRule.Tx(TransactionError.PriorityGreaterThanMaxFee),
                ["SENDER_NOT_EOA"] = RejectionRule.Tx(TransactionError.SenderNotEOA),
                ["NONCE_IS_MAX"] = RejectionRule.Tx(TransactionError.NonceIsMax),
                ["NONCE_MISMATCH_TOO_HIGH"] = RejectionRule.Tx(TransactionError.NonceMismatch),
                ["NONCE_MISMATCH_TOO_LOW"] = RejectionRule.Tx(TransactionError.NonceMismatch),
                ["TYPE_NOT_SUPPORTED"] = RejectionRule.Tx(TransactionError.TransactionTypeNotSupported),
                // AMS-STATE-03. state_tests names the type-1 (EIP-2930) and type-2 (EIP-1559)
                // "used before its fork activated" rejections separately from type-3's - all
                // three are the SAME SupportedTransactionTypeRule/TransactionTypeNotSupported verdict.
                ["TYPE_1_TX_PRE_FORK"] = RejectionRule.Tx(TransactionError.TransactionTypeNotSupported),
                ["TYPE_2_TX_PRE_FORK"] = RejectionRule.Tx(TransactionError.TransactionTypeNotSupported),
                ["TYPE_3_TX_PRE_FORK"] = RejectionRule.Tx(TransactionError.TransactionTypeNotSupported),
                ["TYPE_4_TX_PRE_FORK"] = RejectionRule.Tx(TransactionError.TransactionTypeNotSupported),
                ["TYPE_3_TX_CONTRACT_CREATION"] = RejectionRule.Tx(TransactionError.Type3TxContractCreation),
                ["TYPE_3_TX_ZERO_BLOBS"] = RejectionRule.Tx(TransactionError.Type3TxZeroBlobs),
                ["TYPE_3_TX_INVALID_BLOB_VERSIONED_HASH"] = RejectionRule.Tx(TransactionError.Type3TxInvalidBlobVersionedHash),
                ["TYPE_3_TX_BLOB_COUNT_EXCEEDED"] = RejectionRule.TxOrBlock(
                    new[] { TransactionError.Type3TxBlobCountExceeded },
                    new[] { BlockValidityCheck.BlobGasCapacity }),
                ["TYPE_3_TX_MAX_BLOB_GAS_ALLOWANCE_EXCEEDED"] = RejectionRule.TxOrBlock(
                    new[] { TransactionError.Type3TxBlobCountExceeded },
                    new[] { BlockValidityCheck.BlobGasCapacity }),
                ["TYPE_4_EMPTY_AUTHORIZATION_LIST"] = RejectionRule.Tx(TransactionError.Type4EmptyAuthorizationList),
                ["INVALID_DEPOSIT_EVENT_LAYOUT"] = RejectionRule.Fault<MalformedDepositLogException>(),
                ["TYPE_4_TX_CONTRACT_CREATION"] = RejectionRule.Tx(TransactionError.Type4TxContractCreation),
                ["TYPE_4_TX_EMPTY_AUTHORIZATION_LIST"] = RejectionRule.Tx(TransactionError.Type4EmptyAuthorizationList),
                ["GASLIMIT_PRICE_PRODUCT_OVERFLOW"] = RejectionRule.NotImplemented(
                    "no guard exists on the gasLimit x gasPrice product; the shortfall is only seen as a balance failure, which is a different verdict"),

                // AMS-7928-35. EEST uses ONE label for both halves of EIP-7928 §Engine
                // API's "malformed or doesn't match", so both of our observables satisfy
                ["INVALID_BLOCK_ACCESS_LIST"] = RejectionRule.Block(
                    BlockValidityCheck.BlockAccessListHash,
                    BlockValidityCheck.BlockAccessListMalformed),
                ["INVALID_BAL_HASH"] = RejectionRule.Block(BlockValidityCheck.BlockAccessListHash),

                ["SYSTEM_CONTRACT_CALL_FAILED"] = RejectionRule.SystemCall(),
                ["SYSTEM_CONTRACT_EMPTY"] = RejectionRule.Fault<SystemCallPredeployMissingException>(),

                ["BLOCK_ACCESS_LIST_GAS_LIMIT_EXCEEDED"] = RejectionRule.Block(BlockValidityCheck.BlockAccessListGasLimit),
                ["INCORRECT_BLOCK_FORMAT"] = RejectionRule.Block(
                    BlockValidityCheck.BlockAccessListMalformed,
                    BlockValidityCheck.BlobFieldFormat),
                ["GAS_USED_OVERFLOW"] = RejectionRule.Tx(TransactionError.GasAllowanceExceeded),
                ["INVALID_BLOCK_HASH"] = RejectionRule.NotImplemented(
                    "the importer does not validate the block's declared hash; the host runner only recomputes it on the ACCEPT path"),

                ["INVALID_STATE_ROOT"] = RejectionRule.Block(BlockValidityCheck.StateRoot),
                ["INVALID_RECEIPTS_ROOT"] = RejectionRule.Block(BlockValidityCheck.ReceiptsRoot),
                ["INVALID_LOG_BLOOM"] = RejectionRule.Block(BlockValidityCheck.LogsBloom),
                ["INVALID_GAS_USED"] = RejectionRule.Block(BlockValidityCheck.GasUsed),
                ["INCORRECT_EXCESS_BLOB_GAS"] = RejectionRule.Block(BlockValidityCheck.ExcessBlobGas),
                ["INCORRECT_BLOB_GAS_USED"] = RejectionRule.Block(BlockValidityCheck.BlobGasUsed),
                ["BLOB_GAS_USED_ABOVE_LIMIT"] = RejectionRule.Block(BlockValidityCheck.BlobGasUsed),
                ["INVALID_BASEFEE_PER_GAS"] = RejectionRule.Block(BlockValidityCheck.BaseFee),
                ["INVALID_REQUESTS"] = RejectionRule.Block(BlockValidityCheck.RequestsHash),

                ["INVALID_TRANSACTIONS_ROOT"] = RejectionRule.NotImplemented(NoDimension("the transactions root")),
                ["INVALID_WITHDRAWALS_ROOT"] = RejectionRule.Block(BlockValidityCheck.WithdrawalsRoot),
                ["INVALID_GASLIMIT"] = RejectionRule.Block(BlockValidityCheck.GasLimitBound),
                ["INVALID_BLOCK_NUMBER"] = RejectionRule.NotImplemented(NoHeaderPrecheck),
                ["INVALID_BLOCK_TIMESTAMP_OLDER_THAN_PARENT"] = RejectionRule.NotImplemented(NoHeaderPrecheck),
                ["EXTRA_DATA_TOO_BIG"] = RejectionRule.NotImplemented(NoHeaderPrecheck),
                ["UNKNOWN_PARENT"] = RejectionRule.NotImplemented(NoHeaderPrecheck),
                ["UNKNOWN_PARENT_ZERO"] = RejectionRule.NotImplemented(NoHeaderPrecheck),
                ["IMPORT_IMPOSSIBLE_UNCLES_OVER_PARIS"] = RejectionRule.NotImplemented(NoHeaderPrecheck),
                ["IMPORT_IMPOSSIBLE_DIFFICULTY_OVER_PARIS"] = RejectionRule.NotImplemented(NoHeaderPrecheck),
                ["RLP_STRUCTURES_ENCODING"] = RejectionRule.NotImplemented(NoRlpVerdict),
                ["RLP_WITHDRAWALS_NOT_READ"] = RejectionRule.NotImplemented(NoRlpVerdict),
                ["RLP_INVALID_FIELD_OVERFLOW_64"] = RejectionRule.NotImplemented(NoRlpVerdict),

                ["TR_IntrinsicGas"] = RejectionRule.Tx(TransactionError.IntrinsicGasTooLow),
                ["IntrinsicGas"] = RejectionRule.Tx(TransactionError.IntrinsicGasTooLow),
                ["TR_TypeNotSupported"] = RejectionRule.Tx(TransactionError.TransactionTypeNotSupported),
                ["TR_NoFunds"] = RejectionRule.Tx(TransactionError.InsufficientBalance),
                ["TR_NoFundsX"] = RejectionRule.Tx(TransactionError.InsufficientBalance),
                ["TR_NoFundsOrGas"] = RejectionRule.Tx(TransactionError.InsufficientBalance, TransactionError.IntrinsicGasTooLow),
                ["SenderNotEOA"] = RejectionRule.Tx(TransactionError.SenderNotEOA),
                ["TR_FeeCapLessThanBlocks"] = RejectionRule.Tx(TransactionError.InsufficientMaxFeePerGas),
                ["TR_FeeCapLessThanBlocksORNoFunds"] = RejectionRule.Tx(TransactionError.InsufficientMaxFeePerGas, TransactionError.InsufficientBalance),
                ["TR_FeeCapLessThanBlocksORGasLimitReached"] = RejectionRule.Tx(TransactionError.InsufficientMaxFeePerGas, TransactionError.GasAllowanceExceeded),
                ["TR_TipGtFeeCap"] = RejectionRule.Tx(TransactionError.PriorityGreaterThanMaxFee),
                ["TR_GasLimitReached"] = RejectionRule.Tx(TransactionError.GasAllowanceExceeded),
                ["TR_NonceHasMaxValue"] = RejectionRule.Tx(TransactionError.NonceIsMax),
                ["TR_NonceTooHigh"] = RejectionRule.Tx(TransactionError.NonceMismatch),
                ["TR_NonceTooLow"] = RejectionRule.Tx(TransactionError.NonceMismatch),
                ["TR_InitCodeLimitExceeded"] = RejectionRule.Tx(TransactionError.InitcodeSizeExceeded),
                ["TR_BLOBVERSION_INVALID"] = RejectionRule.Tx(TransactionError.Type3TxInvalidBlobVersionedHash),
                ["TR_EMPTYBLOB"] = RejectionRule.Tx(TransactionError.Type3TxZeroBlobs),
                ["TR_BLOBCREATE"] = RejectionRule.Tx(TransactionError.Type3TxContractCreation),
                ["TR_BLOBLIST_OVERSIZE"] = RejectionRule.Tx(TransactionError.Type3TxBlobCountExceeded),
                ["TR_RLP_WRONGVALUE"] = RejectionRule.Fault<ScalarWiderThanItsFieldException>(),
                ["TR_RLP_LEADINGZEROS"] = RejectionRule.Fault<NonCanonicalScalarRlpException>(),
            };
    }
}
