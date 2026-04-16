using Nethereum.EVM.Execution.Precompiles;

namespace Nethereum.EVM
{
    /// <summary>
    /// Builds a <see cref="HardforkRegistry"/> populated with every Ethereum mainnet
    /// fork (Frontier → Osaka) wired with the supplied precompile backends. Mainnet's
    /// fork sequence, opcode tables, gas rules, intrinsic rules and call-frame rules
    /// are defined entirely in Core; the only thing that varies across consumers is
    /// the concrete crypto backend bundle. Pass whatever backend set matches your
    /// execution environment (Nethereum defaults, Zisk witness-backed, risc0, etc.).
    ///
    /// For the Nethereum default backends use <c>DefaultMainnetHardforkRegistry.Instance</c>
    /// in <c>Nethereum.EVM.Precompiles</c> — it's a pre-built singleton composed from
    /// <c>DefaultPrecompileBackends.Instance</c>.
    /// </summary>
    public static class MainnetHardforkRegistry
    {
        public static HardforkRegistry Build(PrecompileBackends backends)
        {
            if (backends is null) throw new System.ArgumentNullException(nameof(backends));

            var r = new HardforkRegistry();

            // Frontier-class: only precompiles 1-4. FrontierThawing and DaoFork are
            // consensus-only forks (difficulty bomb tweak and DAO state recovery) —
            // EVM behaviour identical to the parent fork, so they alias the same config.
            var frontierPrecompiles = new PrecompileRegistry(
                PrecompileGasCalculatorSets.Frontier,
                PrecompileRegistries.FrontierHandlers(backends.EcRecover, backends.Sha256, backends.Ripemd160));
            r.Register(HardforkName.Frontier, HardforkConfig.Frontier.WithPrecompiles(frontierPrecompiles));
            r.Register(HardforkName.FrontierThawing, HardforkConfig.Frontier.WithPrecompiles(frontierPrecompiles));
            r.Register(HardforkName.Homestead, HardforkConfig.Homestead.WithPrecompiles(frontierPrecompiles));
            r.Register(HardforkName.DaoFork, HardforkConfig.Homestead.WithPrecompiles(frontierPrecompiles));
            r.Register(HardforkName.TangerineWhistle, HardforkConfig.TangerineWhistle.WithPrecompiles(frontierPrecompiles));
            r.Register(HardforkName.SpuriousDragon, HardforkConfig.SpuriousDragon.WithPrecompiles(frontierPrecompiles));

            // Byzantium: adds MODEXP, BN128 ADD/MUL/PAIRING
            r.Register(HardforkName.Byzantium, HardforkConfig.Byzantium
                .WithPrecompiles(PrecompileRegistries.WithGas(
                    PrecompileGasCalculatorSets.Byzantium, backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f)));
            r.Register(HardforkName.Constantinople, HardforkConfig.Constantinople
                .WithPrecompiles(PrecompileRegistries.WithGas(
                    PrecompileGasCalculatorSets.Byzantium, backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f)));
            r.Register(HardforkName.Petersburg, HardforkConfig.Petersburg
                .WithPrecompiles(PrecompileRegistries.WithGas(
                    PrecompileGasCalculatorSets.Byzantium, backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f)));

            // Istanbul: adds BLAKE2F + EIP-1108 BN128 gas repricing.
            // MuirGlacier is difficulty-bomb only — EVM-identical to Istanbul.
            var istanbulPrecompiles = PrecompileRegistries.WithGas(
                PrecompileGasCalculatorSets.Istanbul, backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f);
            r.Register(HardforkName.Istanbul, HardforkConfig.Istanbul.WithPrecompiles(istanbulPrecompiles));
            r.Register(HardforkName.MuirGlacier, HardforkConfig.Istanbul.WithPrecompiles(istanbulPrecompiles));

            // Berlin → Paris share precompile pricing. ArrowGlacier and GrayGlacier
            // are difficulty-bomb only — EVM-identical to London.
            var berlinPrecompiles = PrecompileRegistries.WithGas(
                PrecompileGasCalculatorSets.Berlin, backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f);
            r.Register(HardforkName.Berlin, HardforkConfig.Berlin.WithPrecompiles(berlinPrecompiles));
            r.Register(HardforkName.London, HardforkConfig.London.WithPrecompiles(berlinPrecompiles));
            r.Register(HardforkName.ArrowGlacier, HardforkConfig.London.WithPrecompiles(berlinPrecompiles));
            r.Register(HardforkName.GrayGlacier, HardforkConfig.London.WithPrecompiles(berlinPrecompiles));
            r.Register(HardforkName.Paris, HardforkConfig.Paris.WithPrecompiles(berlinPrecompiles));
            r.Register(HardforkName.Shanghai, HardforkConfig.Shanghai.WithPrecompiles(berlinPrecompiles));

            // Cancun: adds KZG placeholder at 0x0a
            r.Register(HardforkName.Cancun, HardforkConfig.Cancun
                .WithPrecompiles(PrecompileRegistries.CancunBase(
                    backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f)));

            // Prague: adds BLS12-381 placeholders 0x0b..0x11
            r.Register(HardforkName.Prague, HardforkConfig.Prague
                .WithPrecompiles(PrecompileRegistries.PragueBase(
                    backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f)));

            // Osaka: adds P256VERIFY (EIP-7951). Registered only when a P256Verify
            // backend is supplied — environments without one (e.g. zkVMs lacking a
            // P256 binding) register Frontier→Prague only.
            if (backends.P256Verify != null)
            {
                r.Register(HardforkName.Osaka, HardforkConfig.Osaka
                    .WithPrecompiles(PrecompileRegistries.OsakaBase(
                        backends.EcRecover, backends.Sha256, backends.Ripemd160, backends.ModExp, backends.Bn128, backends.Blake2f, backends.P256Verify)));
            }

            return r;
        }
    }
}
