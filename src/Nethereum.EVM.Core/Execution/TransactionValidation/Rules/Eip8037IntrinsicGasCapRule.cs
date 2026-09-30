using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    /// <summary>
    /// EIP-8037 (Amsterdam): "The transaction's <b>intrinsic gas is not higher than
    /// EIP-7825's transaction cap</b>. Concretely,
    /// <c>max(intrinsic_gas, calldata_floor_gas_cost) &lt;= TX_MAX_GAS_LIMIT</c>."
    ///
    /// <para>This rule is the <c>intrinsic_gas</c> operand.
    /// <see cref="TransactionExecutor"/> computes <c>ctx.IntrinsicExecutionGas</c>
    /// before running the validation rule chain, so it is already available here;
    /// the <c>calldata_floor_gas_cost</c> operand is not computed until after the
    /// chain runs and is checked there.</para>
    ///
    /// <para>EIP-8037 states this as an intrinsic-gas condition, so a failure is
    /// reported as <see cref="TransactionError.IntrinsicGasTooLow"/> — the same
    /// reason code as an ordinary intrinsic shortfall (checked against EELS
    /// <c>forks/amsterdam/transactions.py:660-667</c>). The Osaka form of the cap
    /// is a condition on the declared gas limit and keeps its own reason code; see
    /// <see cref="Eip7825RawTxGasCapRule"/> for why that is two classes rather than
    /// one with a constructor flag.</para>
    /// </summary>
    public sealed class Eip8037IntrinsicGasCapRule : ITransactionValidationRule
    {
        public static readonly Eip8037IntrinsicGasCapRule Instance = new Eip8037IntrinsicGasCapRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (ctx.IsCallMode) return;

            if (ctx.IntrinsicExecutionGas > GasConstants.EIP8037_TX_MAX_GAS_LIMIT)
                throw new TransactionValidationException(TransactionError.IntrinsicGasTooLow, "INTRINSIC_GAS_TOO_LOW");
        }
    }
}
