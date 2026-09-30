namespace Nethereum.EVM.Execution.Create
{
    using Nethereum.EVM;

    public interface IContractCreationMaterialiseRule
    {
        void Apply(TransactionExecutionContext ctx, TransactionExecutionResult result);
    }
}
