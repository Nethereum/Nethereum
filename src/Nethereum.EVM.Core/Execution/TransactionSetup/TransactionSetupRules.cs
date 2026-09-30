using System.Collections.Generic;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.TransactionSetup
{
    public sealed class TransactionSetupRules
    {
        private readonly ITransactionSetupRule[] _rules;

        public TransactionSetupRules(IReadOnlyList<ITransactionSetupRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                _rules = new ITransactionSetupRule[0];
                return;
            }
            _rules = new ITransactionSetupRule[rules.Count];
            for (int i = 0; i < rules.Count; i++)
                _rules[i] = rules[i];
        }

#if EVM_SYNC
        public void ApplyAfterNonceIncrement(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            for (int i = 0; i < _rules.Length; i++)
                _rules[i].ApplyAfterNonceIncrement(ctx, result);
        }

        public void ApplyCodeResolution(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            for (int i = 0; i < _rules.Length; i++)
                _rules[i].ApplyCodeResolution(ctx, result);
        }
#else
        public async Task ApplyAfterNonceIncrementAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            for (int i = 0; i < _rules.Length; i++)
                await _rules[i].ApplyAfterNonceIncrementAsync(ctx, result);
        }

        public async Task ApplyCodeResolutionAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            for (int i = 0; i < _rules.Length; i++)
                await _rules[i].ApplyCodeResolutionAsync(ctx, result);
        }
#endif

        public static readonly TransactionSetupRules Empty = new TransactionSetupRules(null);
    }
}
