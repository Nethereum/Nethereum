using Nethereum.Util;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class Eip7825GasCapRule : ITransactionValidationRule
    {
        private static readonly EvmUInt256 MAX_TX_GAS = new EvmUInt256(16_777_216);

        public static readonly Eip7825GasCapRule Instance = new Eip7825GasCapRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            // EIP-7825 caps user-submitted transactions; protocol-level
            // system calls (EIP-4788/2935/7002/7251/6110) bypass it.
            if (ctx.Mode == ExecutionMode.SystemCall) return;

            if (ctx.GasLimit > MAX_TX_GAS)
                throw new TransactionValidationException("GAS_LIMIT_EXCEEDS_MAXIMUM");
        }
    }
}
