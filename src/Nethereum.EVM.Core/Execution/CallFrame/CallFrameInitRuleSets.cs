using System.Collections.Generic;
using Nethereum.EVM.Execution.CallFrame.Rules;

namespace Nethereum.EVM.Execution.CallFrame
{
    public static class CallFrameInitRuleSets
    {
        // Pre-EIP-150: all gas forwarded to subcall (no retention)
        public static readonly CallFrameInitRules Frontier = CallFrameInitRules.Empty;
        public static readonly CallFrameInitRules Homestead = Frontier;

        // EIP-150 (Tangerine Whistle): retain 1/64 of gas
        private static readonly CallFrameInitRules _eip150 = new CallFrameInitRules(
            new List<ICallFrameInitRule> { Eip150GasRetentionRule.Instance });

        public static readonly CallFrameInitRules TangerineWhistle = _eip150;
        public static readonly CallFrameInitRules SpuriousDragon = _eip150;
        public static readonly CallFrameInitRules Byzantium = _eip150;
        public static readonly CallFrameInitRules Constantinople = _eip150;
        public static readonly CallFrameInitRules Petersburg = _eip150;
        public static readonly CallFrameInitRules Istanbul = _eip150;
        public static readonly CallFrameInitRules Berlin = _eip150;
        public static readonly CallFrameInitRules London = _eip150;
        public static readonly CallFrameInitRules Paris = _eip150;
        public static readonly CallFrameInitRules Shanghai = _eip150;
        public static readonly CallFrameInitRules Cancun = _eip150;

        // Prague: EIP-7702 delegation (charges gas) THEN EIP-150 gas retention
        public static readonly CallFrameInitRules Prague = new CallFrameInitRules(
            new List<ICallFrameInitRule>
            {
                Eip7702DelegationRule.Instance,
                Eip150GasRetentionRule.Instance,
            });

        public static readonly CallFrameInitRules Osaka = Prague;

        public static CallFrameInitRules FromName(string hardfork)
        {
            if (string.IsNullOrEmpty(hardfork))
                return Cancun;

            switch (hardfork.ToLowerInvariant())
            {
                case "frontier": return Frontier;
                case "homestead": return Homestead;
                case "prague": return Prague;
                case "osaka": return Osaka;
                default: return _eip150;
            }
        }
    }
}
