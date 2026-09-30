namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class ChainIdValidationRule : ITransactionValidationRule
    {
        public static readonly ChainIdValidationRule Instance = new ChainIdValidationRule();
        private ChainIdValidationRule() { }

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (ctx.DeclaredChainId.HasValue && ctx.DeclaredChainId.Value != ctx.ChainId)
                throw new TransactionValidationException(TransactionError.InvalidChainId, "INVALID_CHAINID");
        }
    }
}
