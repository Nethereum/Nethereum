using Nethereum.EVM;

namespace Nethereum.EVM.Precompiles
{
    public static class DefaultHardforkConfigs
    {
        public static HardforkConfig For(HardforkName fork) => DefaultMainnetHardforkRegistry.Instance.Get(fork);

        public static HardforkConfig Frontier => For(HardforkName.Frontier);
        public static HardforkConfig Homestead => For(HardforkName.Homestead);
        public static HardforkConfig TangerineWhistle => For(HardforkName.TangerineWhistle);
        public static HardforkConfig SpuriousDragon => For(HardforkName.SpuriousDragon);
        public static HardforkConfig Byzantium => For(HardforkName.Byzantium);
        public static HardforkConfig Constantinople => For(HardforkName.Constantinople);
        public static HardforkConfig Petersburg => For(HardforkName.Petersburg);
        public static HardforkConfig Istanbul => For(HardforkName.Istanbul);
        public static HardforkConfig Berlin => For(HardforkName.Berlin);
        public static HardforkConfig London => For(HardforkName.London);
        public static HardforkConfig Paris => For(HardforkName.Paris);
        public static HardforkConfig Shanghai => For(HardforkName.Shanghai);
        public static HardforkConfig Cancun => For(HardforkName.Cancun);
        public static HardforkConfig Prague => For(HardforkName.Prague);
        public static HardforkConfig Osaka => For(HardforkName.Osaka);
        public static HardforkConfig Default => Osaka;
    }
}
