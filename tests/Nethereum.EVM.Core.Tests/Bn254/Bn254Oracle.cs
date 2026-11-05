using System.Numerics;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    internal static class Bn254Oracle
    {
        public static readonly BigInteger U = BigInteger.Parse("4965661367192848881");

        public static readonly BigInteger P =
            36 * BigInteger.Pow(U, 4) + 36 * BigInteger.Pow(U, 3) + 24 * BigInteger.Pow(U, 2) + 6 * U + 1;

        public static readonly BigInteger R =
            36 * BigInteger.Pow(U, 4) + 36 * BigInteger.Pow(U, 3) + 18 * BigInteger.Pow(U, 2) + 6 * U + 1;

        public static readonly BigInteger Lambda = BigInteger.Parse("4407920970296243842393367215006156084916469457145843978461");

        public static readonly BigInteger Beta = BigInteger.Parse("2203960485148121921418603742825762020974279258880205651966");

        public static readonly BigInteger Word = BigInteger.One << 64;

        public static readonly BigInteger Montgomery = BigInteger.One << 256;

        public static BigInteger Mod(BigInteger value, BigInteger modulus)
        {
            var remainder = value % modulus;
            return remainder.Sign < 0 ? remainder + modulus : remainder;
        }

        public static BigInteger Inverse(BigInteger value, BigInteger modulus) =>
            BigInteger.ModPow(Mod(value, modulus), modulus - 2, modulus);

        public static ulong Limb(BigInteger value, int index) => (ulong)((value >> (64 * index)) & (Word - 1));

        public static byte[] ToBytes32(BigInteger value)
        {
            var bytes = value.ToByteArray(true, true);
            var padded = new byte[32];
            System.Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        public static BigInteger FromBytes(byte[] bytes, int offset = 0, int length = 32) =>
            new BigInteger(new System.ReadOnlySpan<byte>(bytes, offset, length), true, true);

        public static bool IsOnCurve(BigInteger x, BigInteger y) =>
            Mod(y * y - x * x * x - 3, P).IsZero;

        public static (BigInteger X, BigInteger Y)? Add((BigInteger X, BigInteger Y)? a, (BigInteger X, BigInteger Y)? b)
        {
            if (a == null) return b;
            if (b == null) return a;
            var (x1, y1) = a.Value;
            var (x2, y2) = b.Value;
            BigInteger slope;
            if (x1 == x2)
            {
                if (Mod(y1 + y2, P).IsZero) return null;
                slope = Mod(3 * x1 * x1 * Inverse(2 * y1, P), P);
            }
            else
            {
                slope = Mod((y2 - y1) * Inverse(x2 - x1, P), P);
            }
            var x3 = Mod(slope * slope - x1 - x2, P);
            return (x3, Mod(slope * (x1 - x3) - y1, P));
        }

        public static (BigInteger X, BigInteger Y)? Multiply((BigInteger X, BigInteger Y)? point, BigInteger scalar)
        {
            (BigInteger X, BigInteger Y)? result = null;
            var addend = point;
            while (!scalar.IsZero)
            {
                if (!scalar.IsEven) result = Add(result, addend);
                addend = Add(addend, addend);
                scalar >>= 1;
            }
            return result;
        }

        public static BigInteger? SquareRoot(BigInteger value) =>
            TrySquareRoot(Mod(value, P));

        private static BigInteger? TrySquareRoot(BigInteger value)
        {
            var root = BigInteger.ModPow(value, (P + 1) / 4, P);
            return Mod(root * root, P) == value ? root : (BigInteger?)null;
        }

        public static byte[] EncodePoint((BigInteger X, BigInteger Y)? point)
        {
            var bytes = new byte[64];
            if (point == null) return bytes;
            System.Array.Copy(ToBytes32(point.Value.X), 0, bytes, 0, 32);
            System.Array.Copy(ToBytes32(point.Value.Y), 0, bytes, 32, 32);
            return bytes;
        }
    }
}
