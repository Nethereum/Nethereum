namespace Nethereum.EVM.Execution.Create.Rules
{
    using Nethereum.EVM;

    public sealed class MaterialiseEmptyOnSuccessRule : IContractCreationMaterialiseRule
    {
        public static readonly MaterialiseEmptyOnSuccessRule Instance = new MaterialiseEmptyOnSuccessRule();
        private MaterialiseEmptyOnSuccessRule() { }

        public void Apply(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
            result.ContractAddress = ctx.ContractAddress;
        }
    }
}
