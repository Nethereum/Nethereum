using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Hardforks.Policies;

namespace Nethereum.EVM.Hardforks
{
    public static class HardforkConfigFromSpec
    {
        public static HardforkConfig Build(HardforkSpec spec)
        {
            return new HardforkConfig
            {
                MaxBlobsPerBlock = spec.MaxBlobsPerBlock,
                TargetBlobsPerBlock = spec.TargetBlobsPerBlock,
                MaxCodeSize = spec.MaxCodeSize,
                MaxInitcodeSize = spec.MaxInitCodeSize,
                ContractInitialNonce = spec.ContractInitialNonce,
                SystemCallGas = spec.SystemCallGas,
                RefundQuotient = spec.RefundQuotient,
                SstoreClearsSchedule = spec.SstoreClearsSchedule,
                SstoreSetRefund = spec.SstoreSetRefund,
                SstoreResetRefund = spec.SstoreResetRefund,

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
                ReceiptCodec = spec.ReceiptCodec,
                HeaderCodec = spec.HeaderCodec,
                TransactionDecoder = spec.TransactionDecoder,
                ReceiptConstruction = spec.ReceiptConstruction,
                EthTransferLogRule = spec.EthTransferLog,

                CleanEmptyAccounts = spec.EmptyAccount.DeletesEmpties,
                BaseFeeApplies = spec.BaseFee.BurnsBaseFee,
                EnforceSstoreGasStipend = spec.EnforceSstoreGasStipend is not null && GasStipendActive(spec.EnforceSstoreGasStipend),
                WarmCoinbase = spec.CoinbaseAccess.ShouldPreWarmCoinbase,
                RejectEfPrefix = spec.CodePrefix.RejectsEfPrefix,
            };
        }

        public static HardforkConfig BuildWithPrecompiles(HardforkSpec spec, IPrecompileExecutorFactory factory)
        {
            var config = Build(spec);
            config.Precompiles = PrecompileRegistries.FromSpec(spec.Precompiles, factory);
            return config;
        }

        private static bool GasStipendActive(SstoreGasStipendPolicy policy)
            => policy.ShouldOog(Gas.GasConstants.CALL_STIPEND);
    }
}
