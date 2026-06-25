using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;
using Nethereum.EVM.Execution.Precompiles.Handlers;
using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Production-host <see cref="IPrecompileExecutorFactory"/> — maps
    /// each <see cref="PrecompileKind"/> to its concrete
    /// <see cref="IPrecompileHandler"/> implementation (backed by the
    /// supplied <see cref="PrecompileBackends"/>) plus its matching
    /// <see cref="IPrecompileGasCalculator"/>.
    ///
    /// <para>Adding a new precompile or gas-formula variant means
    /// adding a <see cref="PrecompileKind"/> enum value and a switch
    /// arm here. The runtime registry is built from the spec via
    /// <see cref="PrecompileRegistries.FromSpec"/>, so the spec and
    /// the runtime cannot disagree.</para>
    ///
    /// <para>Precompiles whose backend isn't available in the core
    /// distribution (KZG point evaluation, BLS12-381) are wired as
    /// <see cref="PlaceholderPrecompile"/>. The KZG and BLS plugin
    /// packages replace these via <c>PrecompileRegistry.WithHandlers</c>.</para>
    /// </summary>
    public sealed class MainnetPrecompileExecutorFactory : IPrecompileExecutorFactory
    {
        private readonly PrecompileBackends _backends;

        public MainnetPrecompileExecutorFactory(PrecompileBackends backends)
        {
            if (backends == null) throw new ArgumentNullException(nameof(backends));
            _backends = backends;
        }

        public IPrecompileHandler GetHandler(PrecompileSpec spec)
        {
            switch (spec.Kind)
            {
                case PrecompileKind.Ecrecover:
                    return new EcRecoverPrecompile(_backends.EcRecover);
                case PrecompileKind.Sha256:
                    return new Sha256Precompile(_backends.Sha256);
                case PrecompileKind.Ripemd160:
                    return new Ripemd160Precompile(_backends.Ripemd160);
                case PrecompileKind.Identity:
                    return new IdentityPrecompile();

                case PrecompileKind.ModExp_Eip198:
                case PrecompileKind.ModExp_Eip2565:
                    return new ModExpPrecompile(_backends.ModExp);
                // EIP-7883 (Osaka) ships with EIP-7823: base/exp/mod each
                // capped at 1024 bytes; oversized inputs must be rejected
                // before the gas formula is evaluated.
                case PrecompileKind.ModExp_Eip7883:
                    return new ModExpPrecompile(_backends.ModExp, enforceEip7823Bounds: true);

                case PrecompileKind.Bn256Add_Eip196:
                case PrecompileKind.Bn256Add_Eip1108:
                    return new Bn128AddPrecompile(_backends.Bn128);
                case PrecompileKind.Bn256Mul_Eip196:
                case PrecompileKind.Bn256Mul_Eip1108:
                    return new Bn128MulPrecompile(_backends.Bn128);
                case PrecompileKind.Bn256Pairing_Eip197:
                case PrecompileKind.Bn256Pairing_Eip1108:
                    return new Bn128PairingPrecompile(_backends.Bn128);

                case PrecompileKind.Blake2:
                    return new Blake2fPrecompile(_backends.Blake2f);

                // KZG (Cancun) — implementation lives in Nethereum.EVM.Precompiles.Kzg.
                // The core distribution wires a placeholder; the plugin package
                // replaces it via PrecompileRegistry.WithHandlers.
                case PrecompileKind.PointEvaluation:
                    return new PlaceholderPrecompile(spec.Address);

                // BLS12-381 (Prague EIP-2537) — implementation in Nethereum.EVM.Precompiles.Bls.
                case PrecompileKind.Bls12381_G1Add:
                case PrecompileKind.Bls12381_G1MultiExp:
                case PrecompileKind.Bls12381_G2Add:
                case PrecompileKind.Bls12381_G2MultiExp:
                case PrecompileKind.Bls12381_Pairing:
                case PrecompileKind.Bls12381_MapFpToG1:
                case PrecompileKind.Bls12381_MapFp2ToG2:
                    return new PlaceholderPrecompile(spec.Address);

                // P256VERIFY (Osaka EIP-7951) — secp256r1 signature verification at 0x100.
                // BouncyCastle ECDsaSigner is a core dependency so no plugin
                // indirection is needed; wire the backend directly.
                case PrecompileKind.P256Verify:
                    return new P256VerifyPrecompile(_backends.P256Verify);

                default:
                    throw new NotSupportedException(
                        $"PrecompileKind.{spec.Kind} is not wired in MainnetPrecompileExecutorFactory.GetHandler");
            }
        }

        public IPrecompileGasCalculator GetGasCalculator(PrecompileSpec spec)
        {
            switch (spec.Kind)
            {
                case PrecompileKind.Ecrecover:
                    return new FixedCostPrecompileGasCalculator(GasConstants.ECRECOVER_GAS);
                case PrecompileKind.Sha256:
                    return new LinearPrecompileGasCalculator(
                        GasConstants.SHA256_BASE_GAS, GasConstants.SHA256_PER_WORD_GAS);
                case PrecompileKind.Ripemd160:
                    return new LinearPrecompileGasCalculator(
                        GasConstants.RIPEMD160_BASE_GAS, GasConstants.RIPEMD160_PER_WORD_GAS);
                case PrecompileKind.Identity:
                    return new LinearPrecompileGasCalculator(
                        GasConstants.IDENTITY_BASE_GAS, GasConstants.IDENTITY_PER_WORD_GAS);

                case PrecompileKind.ModExp_Eip198:
                    return new Eip198ModExpGasCalculator();
                case PrecompileKind.ModExp_Eip2565:
                    return new Eip2565ModExpGasCalculator();
                case PrecompileKind.ModExp_Eip7883:
                    return new Eip7883ModExpGasCalculator();

                case PrecompileKind.Bn256Add_Eip196:
                    return new FixedCostPrecompileGasCalculator(500);
                case PrecompileKind.Bn256Add_Eip1108:
                    return new FixedCostPrecompileGasCalculator(150);
                case PrecompileKind.Bn256Mul_Eip196:
                    return new FixedCostPrecompileGasCalculator(40000);
                case PrecompileKind.Bn256Mul_Eip1108:
                    return new FixedCostPrecompileGasCalculator(6000);
                case PrecompileKind.Bn256Pairing_Eip197:
                    return new Bn128PairingGasCalculator(baseGas: 100000, perPairGas: 80000);
                case PrecompileKind.Bn256Pairing_Eip1108:
                    return new Bn128PairingGasCalculator(baseGas: 45000, perPairGas: 34000);

                case PrecompileKind.Blake2:
                    return new Blake2fGasCalculator();

                case PrecompileKind.PointEvaluation:
                    return new FixedCostPrecompileGasCalculator(GasConstants.KZG_POINT_EVALUATION_GAS);

                case PrecompileKind.Bls12381_G1Add:
                    return new FixedCostPrecompileGasCalculator(GasConstants.BLS12_G1ADD_GAS);
                case PrecompileKind.Bls12381_G1MultiExp:
                    return new Bls12MsmGasCalculator(GasConstants.BLS12_G1MSM_BASE_GAS, pairSize: 160, MsmDiscountTable.G1Discount);
                case PrecompileKind.Bls12381_G2Add:
                    return new FixedCostPrecompileGasCalculator(GasConstants.BLS12_G2ADD_GAS);
                case PrecompileKind.Bls12381_G2MultiExp:
                    return new Bls12MsmGasCalculator(GasConstants.BLS12_G2MSM_BASE_GAS, pairSize: 288, MsmDiscountTable.G2Discount);
                case PrecompileKind.Bls12381_Pairing:
                    return new Bls12PairingGasCalculator(
                        GasConstants.BLS12_PAIRING_BASE_GAS, GasConstants.BLS12_PAIRING_PER_PAIR_GAS);
                case PrecompileKind.Bls12381_MapFpToG1:
                    return new FixedCostPrecompileGasCalculator(GasConstants.BLS12_MAP_FP_TO_G1_GAS);
                case PrecompileKind.Bls12381_MapFp2ToG2:
                    return new FixedCostPrecompileGasCalculator(GasConstants.BLS12_MAP_FP2_TO_G2_GAS);

                case PrecompileKind.P256Verify:
                    return new FixedCostPrecompileGasCalculator(GasConstants.P256VERIFY_GAS);

                default:
                    throw new NotSupportedException(
                        $"PrecompileKind.{spec.Kind} is not wired in MainnetPrecompileExecutorFactory.GetGasCalculator");
            }
        }
    }
}
