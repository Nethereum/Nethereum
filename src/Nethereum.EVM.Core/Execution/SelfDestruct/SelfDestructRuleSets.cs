using Nethereum.EVM.Execution.SelfDestruct.Rules;

namespace Nethereum.EVM.Execution.SelfDestruct
{
    public static class SelfDestructRuleSets
    {
        public static readonly ISelfDestructRule Frontier = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule Homestead = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule TangerineWhistle = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule SpuriousDragon = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule Byzantium = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule Constantinople = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule Petersburg = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule Istanbul = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule Berlin = PreCancunSelfDestructRule.WithRefund24000;
        public static readonly ISelfDestructRule London = PreCancunSelfDestructRule.WithRefund0;
        public static readonly ISelfDestructRule Paris = PreCancunSelfDestructRule.WithRefund0;
        public static readonly ISelfDestructRule Shanghai = PreCancunSelfDestructRule.WithRefund0;
        public static readonly ISelfDestructRule Cancun = Eip6780SelfDestructRule.Instance;
        public static readonly ISelfDestructRule Prague = Eip6780SelfDestructRule.Instance;
        public static readonly ISelfDestructRule Osaka = Eip6780SelfDestructRule.Instance;
    }
}
