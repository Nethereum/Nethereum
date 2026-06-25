using System.Collections.Generic;
using Nethereum.EVM.Execution.TransactionValidation.Rules;

namespace Nethereum.EVM.Execution.TransactionValidation
{
    public static class TransactionValidationRuleSets
    {
        // Pre-Berlin forks reject type-1 (EIP-2930) and type-2 (EIP-1559) txs.
        public static readonly TransactionValidationRules Frontier = new TransactionValidationRules(
            new List<ITransactionValidationRule> { PreLondonTxTypeRule.PreBerlin });
        public static readonly TransactionValidationRules Homestead = Frontier;
        public static readonly TransactionValidationRules Byzantium = Frontier;
        public static readonly TransactionValidationRules Constantinople = Frontier;
        public static readonly TransactionValidationRules Istanbul = Frontier;
        // Berlin accepts type-1 (EIP-2930) but rejects type-2 (EIP-1559).
        public static readonly TransactionValidationRules Berlin = new TransactionValidationRules(
            new List<ITransactionValidationRule> { PreLondonTxTypeRule.BerlinOnly });
        // London onwards accepts all pre-blob types.
        public static readonly TransactionValidationRules London = TransactionValidationRules.Empty;
        public static readonly TransactionValidationRules Shanghai = London;

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
                Eip7594MaxBlobsPerTxRule.Instance,
                Eip7702AuthListValidationRule.Instance,
                Eip7825GasCapRule.Instance,
            });
    }
}
