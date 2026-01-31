using Nethereum.Signer.Bls;

namespace Nethereum.EVM
{
    public static class HardforkConfigBlsExtensions
    {
        public static HardforkConfig WithBlsPrecompiles(this HardforkConfig config, IBls12381Operations blsOperations)
        {
            var blsProvider = new Precompiles.Bls.BlsPrecompileProvider(blsOperations);
            return config.WithPrecompileProviders(blsProvider);
        }
    }
}
