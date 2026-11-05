using System.Numerics;

namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal static class Bn254Constants
    {
        public const ulong Modulus0 = 0x3c208c16d87cfd47UL;
        public const ulong Modulus1 = 0x97816a916871ca8dUL;
        public const ulong Modulus2 = 0xb85045b68181585dUL;
        public const ulong Modulus3 = 0x30644e72e131a029UL;

        public const ulong NegatedModulusInverse = 0x87d20782e4866389UL;

        public const ulong RSquared0 = 0xf32cfc5b538afa89UL;
        public const ulong RSquared1 = 0xb5e71911d44501fbUL;
        public const ulong RSquared2 = 0x47ab1eff0a417ff6UL;
        public const ulong RSquared3 = 0x06d89f71cab8351fUL;

        public const ulong Beta0 = 0x5763473177fffffeUL;
        public const ulong Beta1 = 0xd4f263f1acdb5c4fUL;
        public const ulong Beta2 = 0x59e26bcea0d48bacUL;
        public const ulong Beta3 = 0x0000000000000000UL;

        public static readonly BigInteger Order = BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        public static readonly BigInteger Lambda = BigInteger.Parse("4407920970296243842393367215006156084916469457145843978461");

        public static readonly BigInteger LatticeBasis1X = BigInteger.Parse("147946756881789319010696353538189108491");
        public static readonly BigInteger LatticeBasis1Y = BigInteger.Parse("9931322734385697763");
        public static readonly BigInteger LatticeBasis2X = BigInteger.Parse("-9931322734385697763");
        public static readonly BigInteger LatticeBasis2Y = BigInteger.Parse("147946756881789319000765030803803410728");
    }
}
