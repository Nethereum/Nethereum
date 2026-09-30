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

        public static readonly PrecompileGasCalculators Frontier = new PrecompileGasCalculators(
            new PrecompileGasCalculatorEntry[]
            {
                Entry(0x01, new FixedCostPrecompileGasCalculator(GasConstants.ECRECOVER_GAS)),
                Entry(0x02, new LinearPrecompileGasCalculator(GasConstants.SHA256_BASE_GAS, GasConstants.SHA256_PER_WORD_GAS)),
                Entry(0x03, new LinearPrecompileGasCalculator(GasConstants.RIPEMD160_BASE_GAS, GasConstants.RIPEMD160_PER_WORD_GAS)),
                Entry(0x04, new LinearPrecompileGasCalculator(GasConstants.IDENTITY_BASE_GAS, GasConstants.IDENTITY_PER_WORD_GAS)),
            });

        public static readonly PrecompileGasCalculators Byzantium = Frontier.With(
            Entry(0x05, new Eip198ModExpGasCalculator()),
            Entry(0x06, new FixedCostPrecompileGasCalculator(500)),
            Entry(0x07, new FixedCostPrecompileGasCalculator(40000)),
            Entry(0x08, new Bn128PairingGasCalculator(baseGas: 100000, perPairGas: 80000)));

        public static readonly PrecompileGasCalculators Constantinople = Byzantium;
        public static readonly PrecompileGasCalculators Petersburg = Byzantium;

        public static readonly PrecompileGasCalculators Istanbul = Byzantium.With(
            Entry(0x06, new FixedCostPrecompileGasCalculator(150)),
            Entry(0x07, new FixedCostPrecompileGasCalculator(6000)),
            Entry(0x08, new Bn128PairingGasCalculator(baseGas: 45000, perPairGas: 34000)),
            Entry(0x09, new Blake2fGasCalculator()));

        public static readonly PrecompileGasCalculators Berlin = Istanbul.With(
            Entry(0x05, new Eip2565ModExpGasCalculator()));

        public static readonly PrecompileGasCalculators London = Berlin;
        public static readonly PrecompileGasCalculators Shanghai = Berlin;

        public static readonly PrecompileGasCalculators Cancun = new PrecompileGasCalculators(
            new PrecompileGasCalculatorEntry[]
            {
                Entry(0x01, new FixedCostPrecompileGasCalculator(GasConstants.ECRECOVER_GAS)),
                Entry(0x02, new LinearPrecompileGasCalculator(GasConstants.SHA256_BASE_GAS, GasConstants.SHA256_PER_WORD_GAS)),
                Entry(0x03, new LinearPrecompileGasCalculator(GasConstants.RIPEMD160_BASE_GAS, GasConstants.RIPEMD160_PER_WORD_GAS)),
                Entry(0x04, new LinearPrecompileGasCalculator(GasConstants.IDENTITY_BASE_GAS, GasConstants.IDENTITY_PER_WORD_GAS)),
                Entry(0x05, new Eip2565ModExpGasCalculator()),
                Entry(0x06, new FixedCostPrecompileGasCalculator(150)),
                Entry(0x07, new FixedCostPrecompileGasCalculator(6000)),
                Entry(0x08, new Bn128PairingGasCalculator(baseGas: 45000, perPairGas: 34000)),
                Entry(0x09, new Blake2fGasCalculator()),
                Entry(0x0a, new FixedCostPrecompileGasCalculator(GasConstants.KZG_POINT_EVALUATION_GAS)),
            });

        public static readonly PrecompileGasCalculators Prague = Cancun.With(
            Entry(0x0b, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_G1ADD_GAS)),
            Entry(0x0c, new Bls12MsmGasCalculator(GasConstants.BLS12_G1MSM_BASE_GAS, pairSize: 160, MsmDiscountTable.G1Discount)),
            Entry(0x0d, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_G2ADD_GAS)),
            Entry(0x0e, new Bls12MsmGasCalculator(GasConstants.BLS12_G2MSM_BASE_GAS, pairSize: 288, MsmDiscountTable.G2Discount)),
            Entry(0x0f, new Bls12PairingGasCalculator(
                GasConstants.BLS12_PAIRING_BASE_GAS,
                GasConstants.BLS12_PAIRING_PER_PAIR_GAS)),
            Entry(0x10, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_MAP_FP_TO_G1_GAS)),
            Entry(0x11, new FixedCostPrecompileGasCalculator(GasConstants.BLS12_MAP_FP2_TO_G2_GAS)));

        public static readonly PrecompileGasCalculators Osaka = Prague.With(
            Entry(0x05, new Eip7883ModExpGasCalculator()),
            Entry(0x100, new FixedCostPrecompileGasCalculator(GasConstants.P256VERIFY_GAS)));
    }
}
