using System.Collections.Generic;
using Nethereum.EVM.Execution.TransactionValidation.Rules;

namespace Nethereum.EVM.Execution.TransactionValidation
{
    public static class TransactionValidationRuleSets
    {
        public static readonly TransactionValidationRules Frontier = TransactionValidationRules.Empty;
        public static readonly TransactionValidationRules Homestead = Frontier;
        public static readonly TransactionValidationRules Byzantium = Frontier;
        public static readonly TransactionValidationRules Constantinople = Frontier;
        public static readonly TransactionValidationRules Istanbul = Frontier;
        public static readonly TransactionValidationRules Berlin = Frontier;
        public static readonly TransactionValidationRules London = Frontier;
        public static readonly TransactionValidationRules Shanghai = Frontier;

        public static readonly TransactionValidationRules Cancun = new TransactionValidationRules(
            new List<ITransactionValidationRule> { Eip4844BlobValidationRule.Instance });

        public static readonly TransactionValidationRules Prague = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                Eip4844BlobValidationRule.Instance,
                Eip7702AuthListValidationRule.Instance,
            });

        public static readonly TransactionValidationRules Osaka = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                Eip4844BlobValidationRule.Instance,
                Eip7702AuthListValidationRule.Instance,
                Eip7825GasCapRule.Instance,
            });
    }
}
