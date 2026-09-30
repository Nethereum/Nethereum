using Nethereum.Signer.Bls;

namespace Nethereum.EVM.Precompiles.Bls
{
    public static class Bls12381AwareMainnetHardforkRegistry
    {
        public static HardforkRegistry Build(HardforkRegistry baseRegistry, IBls12381Operations bls)
        {
            if (baseRegistry == null) throw new System.ArgumentNullException(nameof(baseRegistry));
            if (bls == null) throw new System.ArgumentNullException(nameof(bls));

            var result = new HardforkRegistry();
            foreach (var name in baseRegistry.RegisteredNames)
            {
                var config = baseRegistry.Get(name);
                if (name >= HardforkName.Prague && config.Precompiles != null)
                {
                    var upgraded = config.Clone();
                    upgraded.Precompiles = config.Precompiles.WithBlsBackend(bls);
                    result.Register(name, upgraded);
                }
                else
                {
                    result.Register(name, config);
                }
            }
            return result;
        }
    }
}
