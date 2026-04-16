using Nethereum.EVM.Execution.Precompiles.GasCalculators;
using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// Per-fork precompile gas bundles. Each bundle is a fresh
    /// <see cref="PrecompileGasCalculators"/> composition, and later forks
    /// are built by calling <see cref="PrecompileGasCalculators.With"/> on
    /// the prior fork's bundle — <b>composition, not class inheritance</b>.
    ///
    /// Reading a fork bundle top-to-bottom tells you exactly what changed
    /// relative to the previous fork:
    /// <see cref="Prague"/> adds the seven EIP-2537 BLS12-381 precompiles on
    /// top of Cancun; <see cref="Osaka"/> replaces the ModExp calculator
    /// (EIP-7883 repricing) and adds P256VERIFY (EIP-7951) on top of Prague.
    ///
    /// Consumers that want a custom fork shape (e.g. "Cancun + EIP-7883 without
    /// EIP-7623") compose freely:
    /// <code>
    /// var custom = PrecompileGasCalculatorSets.Cancun.With(
    ///     (0x05, new Eip7883ModExpGasCalculator()));
    /// </code>
    /// </summary>
    public static class PrecompileGasCalculatorSets
    {
        private static PrecompileGasCalculatorEntry Entry(int address, IPrecompileGasCalculator calculator)
        {
            return new PrecompileGasCalculatorEntry(address, calculator);
        }

        // Pre-Istanbul precompile gas (Frontier→Constantinople)
        // BN128 at original costs: ADD=500, MUL=40000, PAIRING=100000+80000/pair
        public static readonly PrecompileGasCalculators Frontier = new PrecompileGasCalculators(
            new PrecompileGasCalculatorEntry[]
            {
                Entry(0x01, new FixedCostPrecompileGasCalculator(GasConstants.ECRECOVER_GAS)),
                Entry(0x02, new LinearPrecompileGasCalculator(GasConstants.SHA256_BASE_GAS, GasConstants.SHA256_PER_WORD_GAS)),
                Entry(0x03, new LinearPrecompileGasCalculator(GasConstants.RIPEMD160_BASE_GAS, GasConstants.RIPEMD160_PER_WORD_GAS)),
                Entry(0x04, new LinearPrecompileGasCalculator(GasConstants.IDENTITY_BASE_GAS, GasConstants.IDENTITY_PER_WORD_GAS)),
            });

        // Byzantium adds BN128 + MODEXP at original EIP-198 costs
        public static readonly PrecompileGasCalculators Byzantium = Frontier.With(
            Entry(0x05, new Eip198ModExpGasCalculator()),
            Entry(0x06, new FixedCostPrecompileGasCalculator(500)),
            Entry(0x07, new FixedCostPrecompileGasCalculator(40000)),
            Entry(0x08, new Bn128PairingGasCalculator(baseGas: 100000, perPairGas: 80000)));

        public static readonly PrecompileGasCalculators Constantinople = Byzantium;
        public static readonly PrecompileGasCalculators Petersburg = Byzantium;

        // Istanbul reprices BN128 (EIP-1108) + adds BLAKE2F (EIP-152)
        public static readonly PrecompileGasCalculators Istanbul = Byzantium.With(
            Entry(0x06, new FixedCostPrecompileGasCalculator(150)),
            Entry(0x07, new FixedCostPrecompileGasCalculator(6000)),
            Entry(0x08, new Bn128PairingGasCalculator(baseGas: 45000, perPairGas: 34000)),
            Entry(0x09, new Blake2fGasCalculator()));

        // Berlin reprices MODEXP (EIP-2565)
        public static readonly PrecompileGasCalculators Berlin = Istanbul.With(
            Entry(0x05, new Eip2565ModExpGasCalculator()));

        public static readonly PrecompileGasCalculators London = Berlin;
        public static readonly PrecompileGasCalculators Shanghai = Berlin;

        // Cancun adds KZG
        public static readonly PrecompileGasCalculators Cancun = new PrecompileGasCalculators(
            new PrecompileGasCalculatorEntry[]
            {
                // 0x01 ECRECOVER — Frontier
                Entry(0x01, new FixedCostPrecompileGasCalculator(GasConstants.ECRECOVER_GAS)),
                // 0x02 SHA256 — Frontier
                Entry(0x02, new LinearPrecompileGasCalculator(GasConstants.SHA256_BASE_GAS, GasConstants.SHA256_PER_WORD_GAS)),
                // 0x03 RIPEMD160 — Frontier
                Entry(0x03, new LinearPrecompileGasCalculator(GasConstants.RIPEMD160_BASE_GAS, GasConstants.RIPEMD160_PER_WORD_GAS)),
                // 0x04 IDENTITY — Frontier
                Entry(0x04, new LinearPrecompileGasCalculator(GasConstants.IDENTITY_BASE_GAS, GasConstants.IDENTITY_PER_WORD_GAS)),
                // 0x05 MODEXP — Berlin EIP-2565
                Entry(0x05, new Eip2565ModExpGasCalculator()),
                // 0x06 BN128_ADD — Istanbul EIP-1108
                Entry(0x06, new FixedCostPrecompileGasCalculator(150)),
                // 0x07 BN128_MUL — Istanbul EIP-1108
                Entry(0x07, new FixedCostPrecompileGasCalculator(6000)),
                // 0x08 BN128_PAIRING — Istanbul EIP-1108
                Entry(0x08, new Bn128PairingGasCalculator(baseGas: 45000, perPairGas: 34000)),
                // 0x09 BLAKE2F — Istanbul EIP-152
                Entry(0x09, new Blake2fGasCalculator()),
                // 0x0a KZG point evaluation — Cancun EIP-4844
                Entry(0x0a, new FixedCostPrecompileGasCalculator(GasConstants.KZG_POINT_EVALUATION_GAS)),
            });

        /// <summary>
        /// Prague bundle — Cancun plus the seven EIP-2537 BLS12-381 precompiles
        /// at 0x0b..0x11. Built via <see cref="PrecompileGasCalculators.With(PrecompileGasCalculatorEntry[])"/>
        /// so no class inheritance is involved.
        /// </summary>
        public static readonly PrecompileGasCalculators Prague = Cancun.With(
            Entry(0x0b, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_G1ADD_GAS)),
            Entry(0x0c, new Bls12MsmGasCalculator(GasConstants.BLS12_G1MSM_BASE_GAS, pairSize: 160)),
            Entry(0x0d, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_G2ADD_GAS)),
            Entry(0x0e, new Bls12MsmGasCalculator(GasConstants.BLS12_G2MSM_BASE_GAS, pairSize: 288)),
            Entry(0x0f, new Bls12PairingGasCalculator(
                GasConstants.BLS12_PAIRING_BASE_GAS,
                GasConstants.BLS12_PAIRING_PER_PAIR_GAS)),
            Entry(0x10, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_MAP_FP_TO_G1_GAS)),
            Entry(0x11, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_MAP_FP2_TO_G2_GAS)));

        /// <summary>
        /// Osaka bundle — Prague with the ModExp calculator replaced by the
        /// EIP-7883 variant and P256VERIFY (EIP-7951) added at 0x100. Reading
        /// this bundle is the complete delta from Prague to Osaka for
        /// precompile gas.
        /// </summary>
        public static readonly PrecompileGasCalculators Osaka = Prague.With(
            Entry(0x05, new Eip7883ModExpGasCalculator()),
            Entry(0x100, new FixedCostPrecompileGasCalculator(GasConstants.P256VERIFY_GAS)));
    }
}
