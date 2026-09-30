namespace Nethereum.EVM.Hardforks
{
    public static class HardforkSpecRegistry
    {
        public static readonly HardforkSpec[] All =
        {
            FrontierSpec.Instance,
            HomesteadSpec.Instance,
            TangerineWhistleSpec.Instance,
            SpuriousDragonSpec.Instance,
            ByzantiumSpec.Instance,
            ConstantinopleSpec.Instance,
            PetersburgSpec.Instance,
            IstanbulSpec.Instance,
            BerlinSpec.Instance,
            LondonSpec.Instance,
            ParisSpec.Instance,
            ShanghaiSpec.Instance,
            CancunSpec.Instance,
            PragueSpec.Instance,
            OsakaSpec.Instance,
            OsakaBpo1Spec.Instance,
            OsakaBpo2Spec.Instance,
            AmsterdamSpec.Instance,
        };
    }
}
