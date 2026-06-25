namespace Nethereum.EVM.Execution.Create.Rules
{
    using Nethereum.EVM;

    /// <summary>
    /// Uniform-across-forks materialisation rule for empty-data
    /// CREATE-transactions: commits the snapshot taken in
    /// <c>TransactionExecutor.SetupTargetAccount</c> and records
    /// <see cref="TransactionExecutionResult.ContractAddress"/>.
    /// </summary>
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
