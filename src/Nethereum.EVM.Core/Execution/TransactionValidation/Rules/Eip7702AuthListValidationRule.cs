namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class Eip7702AuthListValidationRule : ITransactionValidationRule
    {
        private const int PER_EMPTY_ACCOUNT_COST = 25000;

        public static readonly Eip7702AuthListValidationRule Instance = new Eip7702AuthListValidationRule();

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (ctx.AuthorisationList == null)
                return;

            if (ctx.IsContractCreation)
                throw new TransactionValidationException("TYPE_4_TX_CONTRACT_CREATION");

            if (ctx.AuthorisationList.Count == 0)
                throw new TransactionValidationException("TYPE_4_EMPTY_AUTHORIZATION_LIST");

            ctx.IntrinsicGas += ctx.AuthorisationList.Count * PER_EMPTY_ACCOUNT_COST;
        }
    }
}
