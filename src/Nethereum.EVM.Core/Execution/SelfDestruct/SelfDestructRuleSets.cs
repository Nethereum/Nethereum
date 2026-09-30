using Nethereum.EVM.Execution.SelfDestruct.Rules;

namespace Nethereum.EVM.Execution.SelfDestruct
{
    public static class SelfDestructRuleSets
    {
        public static readonly ISelfDestructRule Frontier = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule London = PreCancunSelfDestructRule.WithRefund0;
        public static readonly ISelfDestructRule Cancun = Eip6780SelfDestructRule.Instance;
        public static readonly ISelfDestructRule Amsterdam = Eip8246SelfDestructRule.Instance;
    }
}
