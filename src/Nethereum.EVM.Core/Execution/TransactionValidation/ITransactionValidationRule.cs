namespace Nethereum.EVM.Execution.TransactionValidation
{
    public interface ITransactionValidationRule
    {
        void Validate(TransactionExecutionContext ctx, HardforkConfig config);
    }
}
