using Org.BouncyCastle.Math;

namespace Nethereum.Signer.Crypto.BN128
{
    /// <summary>
    /// Constants for BN128 (alt_bn128/BN254) curve pairing operations.
    /// Ported from go-ethereum/crypto/bn256/google/constants.go
    /// </summary>
    public static class BN128Constants
    {
        /// <summary>
        /// Field modulus p = 36u⁴ + 36u³ + 24u² + 6u + 1
        /// </summary>
        public static readonly BigInteger P = new BigInteger("21888242871839275222246405745257275088696311157297823662689037894645226208583");

        /// <summary>
        /// Group order r = 36u⁴ + 36u³ + 18u² + 6u + 1
        /// </summary>
        public static readonly BigInteger Order = new BigInteger("21888242871839275088548364400416034343698204186575808495617");

        /// <summary>
        /// BN parameter u (sometimes called x)
        /// </summary>
        public static readonly BigInteger U = BigInteger.ValueOf(4965661367192848881L);

        /// <summary>
        /// 6u + 2 used in the Miller loop
        /// </summary>
        public static readonly BigInteger SixUPlus2 = U.Multiply(BigInteger.ValueOf(6)).Add(BigInteger.Two);

        /// <summary>
        /// ξ^((p-1)/6) for Frobenius map
        /// Fp2 representation: A=imaginary, B=real
        /// go-ethereum: x=16469...(imag), y=8376...(real)
        /// </summary>
        public static readonly Fp2 XiToPMinus1Over6 = new Fp2(
            new BigInteger("16469823323077808223889137241176536799009286646108169935659301613961712198316"),
            new BigInteger("8376118865763821496583973867626364092589906065868298776909617916018768340080")
        );

        /// <summary>
        /// ξ^((p-1)/3) for Frobenius map
        /// go-ethereum: x=10307...(imag), y=21575...(real)
        /// </summary>
        public static readonly Fp2 XiToPMinus1Over3 = new Fp2(
            new BigInteger("10307601595873709700152284273816112264069230130616436755625194854815875713954"),
            new BigInteger("21575463638280843010398324269430826099269044274347216827212613867836435027261")
        );

        /// <summary>
        /// ξ^((p-1)/2) for Frobenius map
        /// go-ethereum: x=3505...(imag), y=2821...(real)
        /// </summary>
        public static readonly Fp2 XiToPMinus1Over2 = new Fp2(
            new BigInteger("3505843767911556378687030309984248845540243509899259641013678093033130930403"),
            new BigInteger("2821565182194536844548159561693502659359617185244120367078079554186484126554")
        );

        /// <summary>
        /// ξ^((p²-1)/6) for Frobenius P² map (scalar in Fp)
        /// </summary>
        public static readonly BigInteger XiToPSquaredMinus1Over6 = new BigInteger("21888242871839275220042445260109153167277707414472061641714758635765020556617");

        /// <summary>
        /// ξ^((p²-1)/3) for Frobenius P² map (scalar in Fp)
        /// </summary>
        public static readonly BigInteger XiToPSquaredMinus1Over3 = new BigInteger("21888242871839275220042445260109153167277707414472061641714758635765020556616");

        /// <summary>
        /// ξ^((2p²-2)/3) for final exponentiation
        /// </summary>
        public static readonly BigInteger XiTo2PSquaredMinus2Over3 = new BigInteger("2203960485148121921418603742825762020974279258880205651966");

        /// <summary>
        /// ξ^((2p-2)/3) for final exponentiation
        /// go-ethereum: x=19937...(imag), y=2581...(real)
        /// </summary>
        public static readonly Fp2 XiTo2PMinus2Over3 = new Fp2(
            new BigInteger("19937756971775647987995932169929341994314640652964949448313374472400716661030"),
            new BigInteger("2581911344467009335267311115468803099551665605076196740867805258568234346338")
        );

        /// <summary>
        /// Twist curve parameter B' = 3/(9+i) in Fp2
        /// </summary>
        public static readonly Fp2 TwistB = new Fp2(
            new BigInteger("266929791119991161246907387137283842545076965332900288569378510910307636690"),
            new BigInteger("19485874751759354771024239261021720505790618469301721065564631296452457478373")
        );

        /// <summary>
        /// NAF (Non-Adjacent Form) representation of 6u+2 for efficient Miller loop
        /// </summary>
        public static readonly sbyte[] SixUPlus2NAF = new sbyte[]
        {
            0, 0, 0, 1, 0, 1, 0, -1, 0, 0, 1, -1, 0, 0, 1, 0,
            0, 1, 1, 0, -1, 0, 0, 1, 0, -1, 0, 0, 0, 0, 1, 1,
            1, 0, 0, -1, 0, 0, 1, 0, 0, 0, 0, 0, -1, 0, 0, 1,
            1, 0, 0, -1, 0, 0, 0, 1, 1, 0, -1, 0, 0, 1, 0, 1, 1
        };
    }
}
