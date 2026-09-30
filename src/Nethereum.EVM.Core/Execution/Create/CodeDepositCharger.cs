using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Execution.Create
{
    public static class CodeDepositCharger
    {
        public static CodeDepositResult Charge(Program program, byte[] deployedCode, HardforkConfig config, long availableExecutionGas)
        {
            var codeSize = deployedCode?.Length ?? 0;

            if (config.IntrinsicGasRules.StateGasActive)
            {
                var codeWords = (codeSize + 31) / 32;
                var codeHashExecutionGas = (long)codeWords * GasConstants.EIP8037_CODE_HASH_PER_WORD;
                var codeDepositStateGas = (long)codeSize * GasConstants.EIP8037_COST_PER_STATE_BYTE;

                if (availableExecutionGas < codeHashExecutionGas)
                    return new CodeDepositResult { Failed = true };

                program.UpdateGasUsed(codeHashExecutionGas);
                if (!StateGasMeter.TryChargeStateGas(program, codeDepositStateGas, out var spillNeeded))
                    return new CodeDepositResult { Failed = true };

                return new CodeDepositResult
                {
                    Failed = false,
                    FinalCode = deployedCode,
                    FinalCodeDepositCost = codeHashExecutionGas + spillNeeded
                };
            }

            var flatCodeDepositCost = (long)codeSize * GasConstants.CREATE_DATA_GAS;
            if (availableExecutionGas < flatCodeDepositCost)
            {
                var depositResult = config.CodeDepositRule.HandleCodeDepositOOG(new CodeDepositContext
                {
                    Code = deployedCode,
                    GasRemaining = availableExecutionGas,
                    CodeDepositCost = flatCodeDepositCost
                });
                return depositResult.Failed
                    ? new CodeDepositResult { Failed = true }
                    : new CodeDepositResult { Failed = false, FinalCode = depositResult.FinalCode, FinalCodeDepositCost = depositResult.FinalCodeDepositCost };
            }

            return new CodeDepositResult { Failed = false, FinalCode = deployedCode, FinalCodeDepositCost = flatCodeDepositCost };
        }
    }
}
