using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class EestTransactionTestsDriver : IConformanceDriver
    {
        public static readonly EestTransactionTestsDriver Instance = new();

        public string SuiteId => "eest-transaction-tests";
        public string SuiteName => "Nethereum EEST transaction_tests - standalone transaction validity";

        public static readonly HardforkRegistry FixtureRegistry = EestBlockchainTestsRlpDriver.FixtureRegistry;

        private static readonly ITransactionVerificationAndRecovery Verifier = new TransactionVerificationAndRecoveryImp();

        private static readonly StaticBlockEnvironment BlockEnvironment = new();

        public Task<ConformanceCaseResult> RunAsync(TransactionTestLoader.TransactionTest test)
        {
            if (test.Results.Count == 0)
                return Task.FromResult(ConformanceCaseResult.Fail("noResult", $"Test '{test.Name}' has no result forks."));

            ISignedTransaction signedTx = null;
            Exception decodeFault = null;
            try
            {
                signedTx = TransactionFactory.CreateTransaction(test.TxBytes.HexToByteArray(), allowBlobNetworkWrapper: false);
            }
            catch (Exception ex)
            {
                decodeFault = ex;
            }

            foreach (var forkResult in test.Results)
            {
                var outcome = RunFork(test, forkResult, signedTx, decodeFault);
                if (!outcome.Success) return Task.FromResult(outcome);
            }

            return Task.FromResult(ConformanceCaseResult.Ok());
        }

        private static ConformanceCaseResult RunFork(
            TransactionTestLoader.TransactionTest test,
            TransactionTestLoader.ForkResult forkResult,
            ISignedTransaction signedTx,
            Exception decodeFault)
        {
            HardforkName fork;
            try
            {
                fork = HardforkNames.Parse(forkResult.Fork);
            }
            catch (ArgumentException ex)
            {
                return ConformanceCaseResult.Fail("malformedFork",
                    $"result carries an unrecognised fork name '{forkResult.Fork}': {ex.Message}");
            }

            var config = FixtureRegistry.Get(fork);

            if (decodeFault != null)
            {
                if (!forkResult.IsRejection)
                    return ConformanceCaseResult.Fail("decodeFailedButValid",
                        $"[{forkResult.Fork}] transaction is valid per fixture but failed to decode: {decodeFault.GetType().Name}: {decodeFault.Message}");

                if (SatisfiedByRefusalToForm(forkResult.Exception)
                    || ExpectedRejectionMatcher.Satisfies(forkResult.Exception, decodeFault, out _))
                    return ConformanceCaseResult.Ok();

                return ConformanceCaseResult.Fail("rejectionReasonMismatch",
                    $"[{forkResult.Fork}] expected exception=\"{forkResult.Exception}\" but decode threw {decodeFault.GetType().Name}: {decodeFault.Message}");
            }

            string sender;
            try
            {
                sender = Verifier.GetSenderAddress(signedTx);
            }
            catch (Exception ex)
            {
                return forkResult.IsRejection
                       && (SatisfiedByRefusalToForm(forkResult.Exception)
                           || ExpectedRejectionMatcher.Satisfies(forkResult.Exception, ex, out _))
                    ? ConformanceCaseResult.Ok()
                    : ConformanceCaseResult.Fail("senderRecovery", $"[{forkResult.Fork}] {ex.GetType().Name}: {ex.Message}");
            }

            var ctx = TransactionContextFactory.From(signedTx, sender, BlockEnvironment, executionState: null);

            long intrinsicGas;
            try
            {
                intrinsicGas = ValidateAndComputeIntrinsicGas(ctx, config, signedTx);
            }
            catch (TransactionValidationException tve)
            {
                if (forkResult.IsRejection && ExpectedRejectionMatcher.Satisfies(forkResult.Exception, tve, out var detail))
                    return ConformanceCaseResult.Ok();

                return forkResult.IsRejection
                    ? ConformanceCaseResult.Fail("rejectionReasonMismatch", $"[{forkResult.Fork}] expected \"{forkResult.Exception}\" but got {tve.Reason}: {tve.Message}")
                    : ConformanceCaseResult.Fail("validationError", $"[{forkResult.Fork}] unexpected {tve.Reason}: {tve.Message}");
            }

            if (forkResult.IsRejection)
                return ConformanceCaseResult.Fail("rejectionNotEnforced",
                    $"[{forkResult.Fork}] expected exception=\"{forkResult.Exception}\" but the transaction validated");

            return CompareValidFork(forkResult, signedTx, sender, intrinsicGas);
        }

        /// <summary>
        /// A transaction_tests fixture that names a structural/format rejection is satisfied when
        /// Nethereum refuses to even form the transaction - a decode or sender-recovery throw - since
        /// that IS the rejection for a malformed body, EIP-7702 authorisation tuple, or non-canonical
        /// scalar encoding, and a decoder cannot report the structured reason a validity rule would.
        /// Restricted to structural labels so a valid transaction (which decodes cleanly) can never be
        /// passed this way - its sender and intrinsic gas must still match.
        /// </summary>
        private static readonly HashSet<string> StructuralRejectionLabels = new(StringComparer.Ordinal)
        {
            "TYPE_4_INVALID_AUTHORIZATION_FORMAT",
            "TYPE_4_INVALID_AUTHORITY_SIGNATURE",
            "TYPE_4_INVALID_AUTHORITY_SIGNATURE_S_TOO_HIGH",
            "TYPE_3_TX_WITH_FULL_BLOBS",
        };

        private static bool SatisfiedByRefusalToForm(string expectException)
        {
            if (ExpectedRejectionMatcher.SatisfiedByDecodeFailure(expectException)) return true;

            foreach (var alternative in expectException.Split('|'))
            {
                var label = alternative.Trim();
                var dot = label.LastIndexOf('.');
                if (dot >= 0) label = label.Substring(dot + 1);
                if (StructuralRejectionLabels.Contains(label)) return true;
            }

            return false;
        }

        private static long ValidateAndComputeIntrinsicGas(
            TransactionExecutionContext ctx, HardforkConfig config, ISignedTransaction signedTx)
        {
            var isSelfTransfer = !ctx.IsContractCreation && ctx.Sender.IsTheSameAddress(ctx.To);
            var hasValue = !ctx.Value.IsZero;

            config.TransactionValidationRules?.Validate(ctx, config);

            var intrinsicGas = config.IntrinsicGasRules.CalculateIntrinsicGas(
                ctx.Data, ctx.IsContractCreation, ctx.AccessList, isSelfTransfer, hasValue);

            var minimumGas = config.IntrinsicGasRules.CalculateMinimumGasLimit(
                ctx.Data, ctx.IsContractCreation, ctx.AccessList, isSelfTransfer, hasValue);

            if (minimumGas > signedTx.GetGasLimit())
                throw new TransactionValidationException(TransactionError.IntrinsicGasTooLow, "INTRINSIC_GAS_TOO_LOW");

            return intrinsicGas;
        }

        private static ConformanceCaseResult CompareValidFork(
            TransactionTestLoader.ForkResult forkResult, ISignedTransaction signedTx, string sender, long intrinsicGas)
        {
            if (!string.IsNullOrEmpty(forkResult.Sender)
                && !sender.IsTheSameAddress(forkResult.Sender))
            {
                return ConformanceCaseResult.Fail("sender",
                    $"[{forkResult.Fork}] recovered sender {sender} != fixture {forkResult.Sender}");
            }

            if (!string.IsNullOrEmpty(forkResult.IntrinsicGas))
            {
                var expected = forkResult.IntrinsicGas.HexToBigInteger(false);
                if (expected != intrinsicGas)
                    return ConformanceCaseResult.Fail("intrinsicGas",
                        $"[{forkResult.Fork}] computed intrinsicGas {intrinsicGas} != fixture {expected}");
            }

            if (!string.IsNullOrEmpty(forkResult.Hash) && signedTx.Hash is { Length: > 0 })
            {
                var expectedHash = forkResult.Hash.HexToByteArray();
                if (!signedTx.Hash.AreTheSame(expectedHash))
                    return ConformanceCaseResult.Fail("hash",
                        $"[{forkResult.Fork}] tx hash 0x{signedTx.Hash.ToHex()} != fixture {forkResult.Hash}");
            }

            return ConformanceCaseResult.Ok();
        }

        private sealed class StaticBlockEnvironment : IBlockEnvironment
        {
            public EvmUInt256 BlockNumber => EvmUInt256.One;
            public EvmUInt256 Timestamp => EvmUInt256.Zero;
            public string Coinbase => "0x0000000000000000000000000000000000000000";
            public EvmUInt256 BaseFee => EvmUInt256.Zero;
            public EvmUInt256 Difficulty => EvmUInt256.Zero;
            public EvmUInt256 BlockGasLimit => new EvmUInt256((ulong)long.MaxValue);
            public EvmUInt256 ChainId => EvmUInt256.One;
            public EvmUInt256 ExcessBlobGas => EvmUInt256.Zero;
            public ulong? SlotNumber => null;
        }
    }
}
