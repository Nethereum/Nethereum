using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Hardforks.Policies;

namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Adapter that converts a declarative <see cref="HardforkSpec"/> into
    /// the runtime <see cref="HardforkConfig"/> consumed by
    /// <see cref="TransactionExecutor"/> and the EVM simulator.
    ///
    /// <para><b>Why this exists:</b> <see cref="HardforkConfig"/> remains
    /// the runtime surface (so existing consumers — block executors, RPC,
    /// test runners — don't have to change). <see cref="HardforkSpec"/> is
    /// the declarative source-of-truth where every property is required
    /// and every choice is EIP-documented. New forks are added by writing
    /// a new spec file; the adapter turns it into a runtime config with
    /// no behaviour change.</para>
    /// </summary>
    public static class HardforkConfigFromSpec
    {
        public static HardforkConfig Build(HardforkSpec spec)
        {
            return new HardforkConfig
            {
                // Numeric constants — direct copy.
                MaxBlobsPerBlock = spec.MaxBlobsPerBlock,
                MaxCodeSize = spec.MaxCodeSize,
                MaxInitcodeSize = spec.MaxInitCodeSize,
                ContractInitialNonce = spec.ContractInitialNonce,
                RefundQuotient = spec.RefundQuotient,
                SstoreClearsSchedule = spec.SstoreClearsSchedule,
                SstoreSetRefund = spec.SstoreSetRefund,
                SstoreResetRefund = spec.SstoreResetRefund,

                // Strategy references — direct copy.
                GasForwarding = spec.GasForwarding,
                IntrinsicGasRules = spec.IntrinsicGas,
                OpcodeHandlers = spec.Opcodes.Freeze(),
                CodeDepositRule = spec.CodeDeposit,
                ContractCreationMaterialiseRule = spec.ContractCreationMaterialise,
                BlockHashRule = spec.BlockHash,
                CallFrameInitRules = spec.CallFrameInit,
                TransactionValidationRules = spec.Validation,
                TransactionSetupRules = spec.TransactionSetup,
                TouchedEmptyCleanupRule = spec.TouchedEmptyCleanup,
                SstoreRefundRule = spec.SstoreRefund,

                // Policy singletons → existing bool flags.
                // HardforkConfig still uses bool for now; once all callers
                // migrate to consuming policies directly, the bools go away.
                CleanEmptyAccounts = spec.EmptyAccount.DeletesEmpties,
                BaseFeeApplies = spec.BaseFee.BurnsBaseFee,
                EnforceSstoreSentry = spec.EnforceSstoreSentry is not null && SentryActive(spec.EnforceSstoreSentry),
                WarmCoinbase = spec.CoinbaseAccess.ShouldPreWarmCoinbase,
                RejectEfPrefix = spec.CodePrefix.RejectsEfPrefix,
            };
        }

        /// <summary>
        /// Builds a full runtime <see cref="HardforkConfig"/> from a
        /// <see cref="HardforkSpec"/>, including the precompile registry
        /// resolved from <see cref="HardforkSpec.Precompiles"/> via the
        /// supplied <see cref="IPrecompileExecutorFactory"/>.
        /// </summary>
        public static HardforkConfig BuildWithPrecompiles(HardforkSpec spec, IPrecompileExecutorFactory factory)
        {
            var config = Build(spec);
            config.Precompiles = PrecompileRegistries.FromSpec(spec.Precompiles, factory);
            return config;
        }

        // The sentry policy doesn't expose a "should I fire?" boolean
        // directly (it exposes ShouldOog(long)), but the executor checks
        // a bool today. Probe at the canonical threshold to map back.
        private static bool SentryActive(SstoreSentryPolicy policy)
            => policy.ShouldOog(Gas.GasConstants.CALL_STIPEND);
    }
}
