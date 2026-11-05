using Nethereum.EVM;

namespace Nethereum.AppChain.Server
{
    public static class AppChainPrecompileBackends
    {
        public static HardforkRegistry Build() =>
            Nethereum.EVM.Precompiles.Bls.Bls12381AwareMainnetHardforkRegistry.Build(
                Nethereum.EVM.Precompiles.Kzg.KzgAwareMainnetHardforkRegistry.Instance,
                new Nethereum.Signer.Bls.Herumi.Bls12381Operations());
    }
}
