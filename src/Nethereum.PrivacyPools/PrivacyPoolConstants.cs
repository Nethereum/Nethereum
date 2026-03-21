using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public static class PrivacyPoolConstants
    {
        public static readonly BigInteger SNARK_SCALAR_FIELD =
            BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        public const int MAX_TREE_DEPTH = 32;

        public const int COMMITMENT_FIELD_SIZE = 32;

        public static readonly BigInteger BN254_PRIME =
            BigInteger.Parse("21888242871839275222246405745257275088696311157297823662689037894645226208583");
    }
}
