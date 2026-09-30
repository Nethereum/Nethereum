using System.Collections.Generic;
using Nethereum.EVM.Execution.TransactionValidation.Rules;

namespace Nethereum.EVM.Execution.TransactionValidation
{
    public static class TransactionValidationRuleSets
    {
        public static readonly TransactionValidationRules Frontier = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.LegacyOnly, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules Homestead = Frontier;
        public static readonly TransactionValidationRules Byzantium = Frontier;
        public static readonly TransactionValidationRules Constantinople = Frontier;
        public static readonly TransactionValidationRules Istanbul = Frontier;
        public static readonly TransactionValidationRules Berlin = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.UpToAccessList, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules London = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.UpToFeeMarket, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules Shanghai = London;

        public static readonly TransactionValidationRules Cancun = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToBlob,
                Eip4844BlobValidationRule.Instance,
                ChainIdValidationRule.Instance,
            });

        public static readonly TransactionValidationRules Prague = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToSetCode,
                Eip4844BlobValidationRule.Instance,
                Eip7702AuthListValidationRule.Instance,
                ChainIdValidationRule.Instance,
            });

        public static readonly TransactionValidationRules Osaka = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToSetCode,
                Eip4844BlobValidationRule.Instance,
                Eip7594MaxBlobsPerTxRule.Instance,
                Eip7702AuthListValidationRule.Instance,
                Eip7825RawTxGasCapRule.Instance,
                ChainIdValidationRule.Instance,
            });

        public static readonly TransactionValidationRules Amsterdam = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToSetCode,
                Eip4844BlobValidationRule.Instance,
                Eip7594MaxBlobsPerTxRule.Instance,
                Eip2780AuthListValidationRule.Instance,
                Eip8037IntrinsicGasCapRule.Instance,
                ChainIdValidationRule.Instance,
            });
    }
}
