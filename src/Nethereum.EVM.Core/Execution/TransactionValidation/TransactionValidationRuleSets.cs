using System.Collections.Generic;
using Nethereum.EVM.Execution.TransactionValidation.Rules;

namespace Nethereum.EVM.Execution.TransactionValidation
{
    public static class TransactionValidationRuleSets
    {
        public static readonly TransactionValidationRules Frontier = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.LegacyOnly, TransactionSignatureValidationRule.Frontier, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules Homestead = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.LegacyOnly, TransactionSignatureValidationRule.Homestead, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules SpuriousDragon = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.LegacyOnly, TransactionSignatureValidationRule.SpuriousDragon, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules Byzantium = SpuriousDragon;
        public static readonly TransactionValidationRules Constantinople = SpuriousDragon;
        public static readonly TransactionValidationRules Istanbul = SpuriousDragon;
        public static readonly TransactionValidationRules Berlin = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.UpToAccessList, TransactionSignatureValidationRule.SpuriousDragon, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules London = new TransactionValidationRules(
            new List<ITransactionValidationRule> { SupportedTransactionTypeRule.UpToFeeMarket, TransactionSignatureValidationRule.SpuriousDragon, ChainIdValidationRule.Instance });
        public static readonly TransactionValidationRules Shanghai = London;

        public static readonly TransactionValidationRules Cancun = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToBlob,
                ChainIdValidationRule.Instance,
                TransactionSignatureValidationRule.SpuriousDragon,
                Eip4844BlobValidationRule.Instance,
            });

        public static readonly TransactionValidationRules Prague = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToSetCode,
                ChainIdValidationRule.Instance,
                TransactionSignatureValidationRule.SpuriousDragon,
                Eip4844BlobValidationRule.Instance,
                Eip7702AuthListValidationRule.Instance,
            });

        public static readonly TransactionValidationRules Osaka = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToSetCode,
                Eip7825RawTxGasCapRule.Instance,
                ChainIdValidationRule.Instance,
                TransactionSignatureValidationRule.SpuriousDragon,
                Eip4844BlobValidationRule.Instance,
                Eip7594MaxBlobsPerTxRule.Instance,
                Eip7702AuthListValidationRule.Instance,
            });

        public static readonly TransactionValidationRules Amsterdam = new TransactionValidationRules(
            new List<ITransactionValidationRule>
            {
                SupportedTransactionTypeRule.UpToSetCode,
                TransactionSignatureValidationRule.SpuriousDragon,
                Eip4844BlobValidationRule.Instance,
                Eip7594MaxBlobsPerTxRule.Instance,
                Eip2780AuthListValidationRule.Instance,
                Eip8037IntrinsicGasCapRule.Instance,
                ChainIdValidationRule.Instance,
            });
    }
}
