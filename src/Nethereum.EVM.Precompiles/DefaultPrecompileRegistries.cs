using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Precompiles.Backends;

namespace Nethereum.EVM.Precompiles
{
    public static class DefaultPrecompileRegistries
    {
        public static PrecompileRegistry FrontierBase() =>
            new PrecompileRegistry(
                PrecompileGasCalculatorSets.Frontier,
                PrecompileRegistries.FrontierHandlers(
                    DefaultEcRecoverBackend.Instance,
                    DefaultSha256Backend.Instance,
                    DefaultRipemd160Backend.Instance));

        public static PrecompileRegistry ByzantiumBase() =>
            PrecompileRegistries.WithGas(
                PrecompileGasCalculatorSets.Byzantium,
                DefaultEcRecoverBackend.Instance,
                DefaultSha256Backend.Instance,
                DefaultRipemd160Backend.Instance,
                DefaultModExpBackend.Instance,
                DefaultBn128Backend.Instance,
                DefaultBlake2fBackend.Instance);

        public static PrecompileRegistry IstanbulBase() =>
            PrecompileRegistries.WithGas(
                PrecompileGasCalculatorSets.Istanbul,
                DefaultEcRecoverBackend.Instance,
                DefaultSha256Backend.Instance,
                DefaultRipemd160Backend.Instance,
                DefaultModExpBackend.Instance,
                DefaultBn128Backend.Instance,
                DefaultBlake2fBackend.Instance);

        public static PrecompileRegistry BerlinBase() =>
            PrecompileRegistries.WithGas(
                PrecompileGasCalculatorSets.Berlin,
                DefaultEcRecoverBackend.Instance,
                DefaultSha256Backend.Instance,
                DefaultRipemd160Backend.Instance,
                DefaultModExpBackend.Instance,
                DefaultBn128Backend.Instance,
                DefaultBlake2fBackend.Instance);

        public static PrecompileRegistry CancunBase() =>
            PrecompileRegistries.CancunBase(
                DefaultEcRecoverBackend.Instance,
                DefaultSha256Backend.Instance,
                DefaultRipemd160Backend.Instance,
                DefaultModExpBackend.Instance,
                DefaultBn128Backend.Instance,
                DefaultBlake2fBackend.Instance);

        public static PrecompileRegistry PragueBase() =>
            PrecompileRegistries.PragueBase(
                DefaultEcRecoverBackend.Instance,
                DefaultSha256Backend.Instance,
                DefaultRipemd160Backend.Instance,
                DefaultModExpBackend.Instance,
                DefaultBn128Backend.Instance,
                DefaultBlake2fBackend.Instance);

        public static PrecompileRegistry OsakaBase() =>
            PrecompileRegistries.OsakaBase(
                DefaultEcRecoverBackend.Instance,
                DefaultSha256Backend.Instance,
                DefaultRipemd160Backend.Instance,
                DefaultModExpBackend.Instance,
                DefaultBn128Backend.Instance,
                DefaultBlake2fBackend.Instance,
                DefaultP256VerifyBackend.Instance);
    }
}
