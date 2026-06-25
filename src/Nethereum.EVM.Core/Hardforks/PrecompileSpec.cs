namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Declarative reference to an active precompile at a fork. Specifies:
    /// <list type="number">
    ///   <item>The precompile <see cref="Address"/> slot (e.g. 0x05 for ModExp).</item>
    ///   <item>The <see cref="Kind"/> — which EIP defines the gas formula
    ///   AND identifies the implementation contract. The host registry
    ///   maps <see cref="Kind"/> to a concrete executor lazily.</item>
    /// </list>
    ///
    /// <para>The actual cryptographic implementation (e.g. the BN254
    /// pairing engine) is provided by the EVM-type-specific
    /// <see cref="IPrecompileExecutorRegistry"/>:</para>
    /// <list type="bullet">
    ///   <item>Production Nethereum host: full crypto libraries.</item>
    ///   <item>Zisk zkVM guest: precompile-as-CSR or stripped impl.</item>
    ///   <item>In-memory simulator: a third variant.</item>
    /// </list>
    ///
    /// <para>The executor is resolved <b>lazily</b> on first invocation —
    /// the spec carries only the declarative metadata. Forks that don't
    /// activate a precompile simply don't list its <see cref="PrecompileSpec"/>.</para>
    ///
    /// <para><b>Zisk safety:</b> a small immutable record. The
    /// declarative metadata is part of the witness; the executor
    /// resolution + cryptographic work is the host's concern.</para>
    /// </summary>
    public sealed record PrecompileSpec
    {
        /// <summary>
        /// The address slot of the precompile, as an integer
        /// (e.g. 0x05 for ModExp, 0x100 for P256VERIFY at Osaka).
        /// </summary>
        public required int Address { get; init; }

        /// <summary>
        /// Identifies which EIP defines the gas formula and the
        /// implementation contract semantics. The host registry maps
        /// this enum to a concrete executor.
        /// </summary>
        public required PrecompileKind Kind { get; init; }

        /// <summary>
        /// Convenience constructor for spec readability:
        /// <c>new(0x05, PrecompileKind.ModExp_Eip2565)</c>.
        /// </summary>
        public PrecompileSpec() { }
    }

    /// <summary>
    /// Enumerates each precompile + its specific gas-formula variant.
    /// Variants distinguish between different gas formulas for the same
    /// underlying implementation (e.g. ModExp at Byzantium vs Berlin
    /// EIP-2565 vs Osaka EIP-7883).
    ///
    /// <para>Adding a new precompile = add an enum member + register
    /// its gas formula + executor in the host registry.</para>
    /// </summary>
    public enum PrecompileKind
    {
        /// <summary>0x01 ECDSA secp256k1 signature recovery — Frontier.</summary>
        Ecrecover = 1,

        /// <summary>0x02 SHA-256 hash — Frontier.</summary>
        Sha256,

        /// <summary>0x03 RIPEMD-160 hash — Frontier.</summary>
        Ripemd160,

        /// <summary>0x04 byte-copy identity function — Frontier.</summary>
        Identity,

        /// <summary>0x05 modular exponentiation with EIP-198 (Byzantium) gas formula.</summary>
        ModExp_Eip198,

        /// <summary>0x05 modular exponentiation with EIP-2565 (Berlin) gas reduction.</summary>
        ModExp_Eip2565,

        /// <summary>0x05 modular exponentiation with EIP-7883 (Osaka) gas repricing.</summary>
        ModExp_Eip7883,

        /// <summary>0x06 alt_bn128 G1 add with Byzantium pricing (EIP-196).</summary>
        Bn256Add_Eip196,

        /// <summary>0x06 alt_bn128 G1 add with Istanbul reduction (EIP-1108).</summary>
        Bn256Add_Eip1108,

        /// <summary>0x07 alt_bn128 G1 scalar mul with Byzantium pricing (EIP-196).</summary>
        Bn256Mul_Eip196,

        /// <summary>0x07 alt_bn128 G1 scalar mul with Istanbul reduction (EIP-1108).</summary>
        Bn256Mul_Eip1108,

        /// <summary>0x08 alt_bn128 pairing with Byzantium pricing (EIP-197).</summary>
        Bn256Pairing_Eip197,

        /// <summary>0x08 alt_bn128 pairing with Istanbul reduction (EIP-1108).</summary>
        Bn256Pairing_Eip1108,

        /// <summary>0x09 BLAKE2b compression — Istanbul (EIP-152).</summary>
        Blake2,

        /// <summary>0x0A KZG point-evaluation — Cancun (EIP-4844).</summary>
        PointEvaluation,

        /// <summary>0x0B–0x11 BLS12-381 — Prague (EIP-2537).</summary>
        Bls12381_G1Add,
        Bls12381_G1MultiExp,
        Bls12381_G2Add,
        Bls12381_G2MultiExp,
        Bls12381_Pairing,
        Bls12381_MapFpToG1,
        Bls12381_MapFp2ToG2,

        /// <summary>0x100 secp256r1 (NIST P-256) signature verification — Osaka (EIP-7951).</summary>
        P256Verify,
    }
}
