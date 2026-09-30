using Nethereum.EVM.Gas;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class Eip7825RawTxGasCapRule : ITransactionValidationRule
    {
        public static readonly Eip7825RawTxGasCapRule Instance = new Eip7825RawTxGasCapRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (ctx.IsCallMode) return;

            if (ctx.GasLimit > new EvmUInt256(GasConstants.EIP8037_TX_MAX_GAS_LIMIT))
                throw new TransactionValidationException(TransactionError.GasLimitExceedsMaximum, "GAS_LIMIT_EXCEEDS_MAXIMUM");
        }
    }
}
