using System.Collections.Generic;
using Nethereum.EVM.Execution.TransactionSetup.Rules;

namespace Nethereum.EVM.Execution.TransactionSetup
{
    public static class TransactionSetupRuleSets
    {
        public static readonly TransactionSetupRules Frontier = TransactionSetupRules.Empty;
        public static readonly TransactionSetupRules Homestead = Frontier;
        public static readonly TransactionSetupRules Byzantium = Frontier;
        public static readonly TransactionSetupRules Constantinople = Frontier;
        public static readonly TransactionSetupRules Istanbul = Frontier;
        public static readonly TransactionSetupRules Berlin = Frontier;
        public static readonly TransactionSetupRules London = Frontier;
        public static readonly TransactionSetupRules Shanghai = Frontier;
        public static readonly TransactionSetupRules Cancun = Frontier;

        public static readonly TransactionSetupRules Prague = new TransactionSetupRules(
            new List<ITransactionSetupRule> { Eip7702TransactionSetupRule.Instance });

        public static readonly TransactionSetupRules Osaka = Prague;
    }
}
