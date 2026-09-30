using System.Collections.Generic;

namespace Nethereum.EVM.Execution.TransactionValidation
{
    public sealed class TransactionValidationRules
    {
        private readonly ITransactionValidationRule[] _rules;

        public TransactionValidationRules(IReadOnlyList<ITransactionValidationRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                _rules = new ITransactionValidationRule[0];
                return;
            }
            _rules = new ITransactionValidationRule[rules.Count];
            for (int i = 0; i < rules.Count; i++)
                _rules[i] = rules[i];
        }

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            for (int i = 0; i < _rules.Length; i++)
                _rules[i].Validate(ctx, config);
        }

        public static readonly TransactionValidationRules Empty = new TransactionValidationRules(new ITransactionValidationRule[0]);
    }
}
