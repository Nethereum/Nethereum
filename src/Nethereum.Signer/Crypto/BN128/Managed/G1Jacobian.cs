
namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal readonly struct G1Jacobian
    {
        public const int AffineByteLength = 64;

        public readonly Fp X;
        public readonly Fp Y;
        public readonly Fp Z;

        private G1Jacobian(Fp x, Fp y, Fp z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly G1Jacobian Infinity = new G1Jacobian(Fp.One, Fp.One, Fp.Zero);

        public bool IsInfinity => Z.IsZero;

        public static bool TryFromAffineBytes(byte[] data, int offset, out G1Jacobian point)
        {
            point = Infinity;
            if (!Fp.TryFromBigEndian(data, offset, out var x)) return false;
            if (!Fp.TryFromBigEndian(data, offset + Fp.ByteLength, out var y)) return false;
            if (x.IsZero && y.IsZero) return true;
            if (!IsOnCurve(x, y)) return false;
            point = new G1Jacobian(x, y, Fp.One);
            return true;
        }

        public byte[] ToAffineBytes()
        {
            var bytes = new byte[AffineByteLength];
            if (IsInfinity) return bytes;
            var zInverse = Z.Inverse();
            var zInverseSquared = zInverse.Square();
            (X * zInverseSquared).WriteBigEndian(bytes, 0);
            (Y * zInverseSquared * zInverse).WriteBigEndian(bytes, Fp.ByteLength);
            return bytes;
        }

        public G1Jacobian Negate() => new G1Jacobian(X, -Y, Z);

        public G1Jacobian ApplyEndomorphism() => new G1Jacobian(X * Fp.Beta, Y, Z);

        public G1Jacobian Double()
        {
            if (IsInfinity) return this;
            var a = X.Square();
            var b = Y.Square();
            var c = b.Square();
            var d = ((X + b).Square() - a - c).Double();
            var e = a + a.Double();
            var f = e.Square();
            var x3 = f - d.Double();
            var y3 = e * (d - x3) - c.Double().Double().Double();
            var z3 = (Y * Z).Double();
            return new G1Jacobian(x3, y3, z3);
        }

        public G1Jacobian Add(G1Jacobian other)
        {
            if (IsInfinity) return other;
            if (other.IsInfinity) return this;

            var z1z1 = Z.Square();
            var z2z2 = other.Z.Square();
            var u1 = X * z2z2;
            var u2 = other.X * z1z1;
            var s1 = Y * other.Z * z2z2;
            var s2 = other.Y * Z * z1z1;

            var h = u2 - u1;
            var r = (s2 - s1).Double();
            if (h.IsZero) return r.IsZero ? Double() : Infinity;

            var i = h.Double().Square();
            var j = h * i;
            var v = u1 * i;
            var x3 = r.Square() - j - v.Double();
            var y3 = r * (v - x3) - (s1 * j).Double();
            var z3 = ((Z + other.Z).Square() - z1z1 - z2z2) * h;
            return new G1Jacobian(x3, y3, z3);
        }

        private static bool IsOnCurve(Fp x, Fp y) => y.Square().Equals(x.Square() * x + Fp.Three);
    }
}
