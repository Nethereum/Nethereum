using System;
using Nethereum.EVM.Execution.Precompiles;

namespace Nethereum.EVM
{
    public static class HardforkConfigExtensions
    {
        public static HardforkConfig WithPrecompiles(
            this HardforkConfig config,
            PrecompileRegistry registry)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            return new HardforkConfig
            {
                MaxBlobsPerBlock = config.MaxBlobsPerBlock,
                Precompiles = registry,
                IntrinsicGasRules = config.IntrinsicGasRules,
                OpcodeHandlers = config.OpcodeHandlers,
                CallFrameInitRules = config.CallFrameInitRules,
                TransactionValidationRules = config.TransactionValidationRules,
                TransactionSetupRules = config.TransactionSetupRules,
                CodeDepositRule = config.CodeDepositRule,
                GasForwarding = config.GasForwarding,
                MaxCodeSize = config.MaxCodeSize,
                MaxInitcodeSize = config.MaxInitcodeSize,
                RejectEfPrefix = config.RejectEfPrefix,
                ContractInitialNonce = config.ContractInitialNonce,
                RefundQuotient = config.RefundQuotient,
                SstoreClearsSchedule = config.SstoreClearsSchedule,
                CleanEmptyAccounts = config.CleanEmptyAccounts
            };
        }
    }
}
