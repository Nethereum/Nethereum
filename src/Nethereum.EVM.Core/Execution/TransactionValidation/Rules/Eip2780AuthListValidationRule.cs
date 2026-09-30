using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class Eip2780AuthListValidationRule : ITransactionValidationRule
    {
        private const long AUTH_TUPLE_BYTES = 101;

        private const long EXECUTION_PER_AUTH_BASE_COST =
            AUTH_TUPLE_BYTES * GasConstants.EIP7976_FLOOR_PER_TOKEN_GAS
            + GasConstants.ECRECOVER_GAS
            + GasConstants.EIP8038_COLD_ACCOUNT_ACCESS
            + 2 * GasConstants.WARM_STORAGE_READ_COST;

        public static readonly Eip2780AuthListValidationRule Instance = new Eip2780AuthListValidationRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (ctx.AuthorisationList == null)
                return;

            if (ctx.IsContractCreation)
                throw new TransactionValidationException(TransactionError.Type4TxContractCreation, "TYPE_4_TX_CONTRACT_CREATION");

            if (ctx.AuthorisationList.Count == 0)
                throw new TransactionValidationException(TransactionError.Type4EmptyAuthorizationList, "TYPE_4_EMPTY_AUTHORIZATION_LIST");

            ctx.IntrinsicExecutionGas += ctx.AuthorisationList.Count * EXECUTION_PER_AUTH_BASE_COST;
        }
    }
}
