namespace Nethereum.EVM.Execution.Create
{
    using Nethereum.EVM;

    /// <summary>
    /// CREATE-transaction post-execution materialisation strategy. Invoked by
    /// <c>TransactionExecutor.ExecuteTransaction</c> for contract-creation
    /// transactions that succeed without running init code (empty init data
    /// and zero value): commits the snapshot taken in <c>SetupTargetAccount</c>
    /// so the empty contract account (already added to <c>AccountsState</c>
    /// via <c>PrepareNewContractAccount</c>) is persisted, and records
    /// <see cref="TransactionExecutionResult.ContractAddress"/>.
    /// </summary>
    public interface IContractCreationMaterialiseRule
    {
        void Apply(TransactionExecutionContext ctx, TransactionExecutionResult result);
    }
}
