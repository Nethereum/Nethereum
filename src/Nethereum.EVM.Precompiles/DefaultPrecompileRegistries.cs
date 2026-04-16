using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Precompiles.Backends;

namespace Nethereum.EVM.Precompiles
{
    /// <summary>
    /// Zero-arg fork factories that wire the production managed crypto
    /// backends (EthECKey, BouncyCastle, BN128Curve, managed Blake2f,
    /// <see cref="System.Numerics.BigInteger"/> ModPow, BouncyCastle
    /// ECDsaSigner for P-256) into the portable
    /// <see cref="PrecompileRegistries"/> factories from
    /// <c>Nethereum.EVM.Core</c>.
    ///
    /// Typical usage:
    /// <code>
    /// var config = HardforkConfig.Prague
    ///     .WithPrecompiles(DefaultPrecompileRegistries.PragueBase())
    ///     .WithBlsBackend(new Herumi.Bls12381Operations());
    /// </code>
    ///
    /// Consumers that want to replace one or more backends can call the
    /// parameterised factories on <see cref="PrecompileRegistries"/>
    /// directly, passing a mix of defaults and custom backends.
    /// </summary>
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
