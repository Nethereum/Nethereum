namespace Nethereum.EVM.Hardforks
{
    public sealed record PrecompileSpec
    {
        public required int Address { get; init; }

        public required PrecompileKind Kind { get; init; }

        public PrecompileSpec() { }
    }

    public enum PrecompileKind
    {
        Ecrecover = 1,

        Sha256,

        Ripemd160,

        Identity,

        ModExp_Eip198,

        ModExp_Eip2565,

        ModExp_Eip7883,

        Bn256Add_Eip196,

        Bn256Add_Eip1108,

        Bn256Mul_Eip196,

        Bn256Mul_Eip1108,

        Bn256Pairing_Eip197,

        Bn256Pairing_Eip1108,

        Blake2,

        PointEvaluation,

        Bls12381_G1Add,
        Bls12381_G1MultiExp,
        Bls12381_G2Add,
        Bls12381_G2MultiExp,
        Bls12381_Pairing,
        Bls12381_MapFpToG1,
        Bls12381_MapFp2ToG2,

        P256Verify,
    }
}
