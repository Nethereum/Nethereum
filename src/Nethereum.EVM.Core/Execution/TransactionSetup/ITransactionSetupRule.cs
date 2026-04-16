#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.TransactionSetup
{
    public interface ITransactionSetupRule
    {
#if EVM_SYNC
        void ApplyAfterNonceIncrement(TransactionExecutionContext ctx, TransactionExecutionResult result);
        void ApplyCodeResolution(TransactionExecutionContext ctx, TransactionExecutionResult result);
#else
        Task ApplyAfterNonceIncrementAsync(TransactionExecutionContext ctx, TransactionExecutionResult result);
        Task ApplyCodeResolutionAsync(TransactionExecutionContext ctx, TransactionExecutionResult result);
#endif
    }

    public abstract class TransactionSetupRuleBase : ITransactionSetupRule
    {
#if EVM_SYNC
        public virtual void ApplyAfterNonceIncrement(TransactionExecutionContext ctx, TransactionExecutionResult result) { }
        public virtual void ApplyCodeResolution(TransactionExecutionContext ctx, TransactionExecutionResult result) { }
#else
        public virtual Task ApplyAfterNonceIncrementAsync(TransactionExecutionContext ctx, TransactionExecutionResult result) => Task.FromResult(0);
        public virtual Task ApplyCodeResolutionAsync(TransactionExecutionContext ctx, TransactionExecutionResult result) => Task.FromResult(0);
#endif
    }
}
