using Nethereum.EVM;
using Nethereum.EVM.Precompiles.Bls;
using Nethereum.EVM.Precompiles.Kzg;

namespace Nethereum.MainnetChain
{
    public static class MainnetChainHardforkRegistry
    {
        public static readonly HardforkRegistry Instance =
            Bls12381AwareMainnetHardforkRegistry.Build(
                KzgAwareMainnetHardforkRegistry.Instance,
                new Nethereum.Signer.Bls.Herumi.Bls12381Operations());
    }
}
