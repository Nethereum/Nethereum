using System;
using mcl;

namespace Nethereum.Signer.Bls.Herumi
{
    public class Bls12381Operations : IBls12381Operations
    {
        private static readonly object InitLock = new object();
        private static bool _initialized;

        public const int EIP2537_G1_POINT_SIZE = 128;
        public const int EIP2537_G2_POINT_SIZE = 256;
        public const int EIP2537_FP_SIZE = 64;
        public const int EIP2537_FP2_SIZE = 128;
        public const int EIP2537_SCALAR_SIZE = 32;

        public const int MCL_FP_SIZE = 48;
        public const int MCL_G1_SERIALIZED_SIZE = 48;
        public const int MCL_G2_SERIALIZED_SIZE = 96;

        public Bls12381Operations()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;
                BLS.Init(BLS.BLS12_381);
                _initialized = true;
            }
        }

        public byte[] G1Add(byte[] p1, byte[] p2)
        {
            var point1 = DecodeG1(p1);
            var point2 = DecodeG1(p2);

            var result = new MclBindings.MclBnG1();
            MclBindings.mclBnG1_add(ref result, point1, point2);

            return EncodeG1(result);
        }

        public byte[] G1Mul(byte[] point, byte[] scalar)
        {
            var g1Point = DecodeG1(point);
            var scalarFr = DecodeScalar(scalar);

            var result = new MclBindings.MclBnG1();
            MclBindings.mclBnG1_mul(ref result, g1Point, scalarFr);

            return EncodeG1(result);
        }

        public byte[] G1Msm(byte[][] points, byte[][] scalars)
        {
            if (points.Length != scalars.Length)
                throw new ArgumentException("Points and scalars must have same length");
            if (points.Length == 0)
                throw new ArgumentException("Empty input");

            var g1Points = new MclBindings.MclBnG1[points.Length];
            var scalarFrs = new MclBindings.MclBnFr[scalars.Length];

            for (int i = 0; i < points.Length; i++)
            {
                g1Points[i] = DecodeG1(points[i]);
                scalarFrs[i] = DecodeScalar(scalars[i]);
            }

            var result = new MclBindings.MclBnG1();
            MclBindings.mclBnG1_mulVec(ref result, g1Points[0], scalarFrs[0], (ulong)points.Length);

            return EncodeG1(result);
        }

        public byte[] G2Add(byte[] p1, byte[] p2)
        {
            var point1 = DecodeG2(p1);
            var point2 = DecodeG2(p2);

            var result = new MclBindings.MclBnG2();
            MclBindings.mclBnG2_add(ref result, point1, point2);

            return EncodeG2(result);
        }

        public byte[] G2Mul(byte[] point, byte[] scalar)
        {
            var g2Point = DecodeG2(point);
            var scalarFr = DecodeScalar(scalar);

            var result = new MclBindings.MclBnG2();
            MclBindings.mclBnG2_mul(ref result, g2Point, scalarFr);

            return EncodeG2(result);
        }

        public byte[] G2Msm(byte[][] points, byte[][] scalars)
        {
            if (points.Length != scalars.Length)
                throw new ArgumentException("Points and scalars must have same length");
            if (points.Length == 0)
                throw new ArgumentException("Empty input");

            var g2Points = new MclBindings.MclBnG2[points.Length];
            var scalarFrs = new MclBindings.MclBnFr[scalars.Length];

            for (int i = 0; i < points.Length; i++)
            {
                g2Points[i] = DecodeG2(points[i]);
                scalarFrs[i] = DecodeScalar(scalars[i]);
            }

            var result = new MclBindings.MclBnG2();
            MclBindings.mclBnG2_mulVec(ref result, g2Points[0], scalarFrs[0], (ulong)points.Length);

            return EncodeG2(result);
        }

        public bool Pairing(byte[][] g1Points, byte[][] g2Points)
        {
            if (g1Points.Length != g2Points.Length)
                throw new ArgumentException("G1 and G2 point arrays must have same length");

            if (g1Points.Length == 0)
                return true;

            var g1Array = new MclBindings.MclBnG1[g1Points.Length];
            var g2Array = new MclBindings.MclBnG2[g2Points.Length];

            for (int i = 0; i < g1Points.Length; i++)
            {
                g1Array[i] = DecodeG1(g1Points[i]);
                g2Array[i] = DecodeG2(g2Points[i]);
            }

            var gt = new MclBindings.MclBnGT();
            MclBindings.mclBn_millerLoopVec(ref gt, g1Array[0], g2Array[0], (ulong)g1Points.Length);
            MclBindings.mclBn_finalExp(ref gt, gt);

            return MclBindings.mclBnGT_isOne(gt) == 1;
        }

        public byte[] MapFpToG1(byte[] fp)
        {
            if (fp.Length != EIP2537_FP_SIZE)
                throw new ArgumentException($"Invalid Fp length: expected {EIP2537_FP_SIZE}, got {fp.Length}");

            var fpElement = DecodeFp(fp);
            var result = new MclBindings.MclBnG1();

            int ret = MclBindings.mclBnFp_mapToG1(ref result, fpElement);
            if (ret != 0)
                throw new ArgumentException("Failed to map Fp to G1");

            return EncodeG1(result);
        }

        public byte[] MapFp2ToG2(byte[] fp2)
        {
            if (fp2.Length != EIP2537_FP2_SIZE)
                throw new ArgumentException($"Invalid Fp2 length: expected {EIP2537_FP2_SIZE}, got {fp2.Length}");

            var fp2Element = DecodeFp2(fp2);
            var result = new MclBindings.MclBnG2();

            int ret = MclBindings.mclBnFp2_mapToG2(ref result, fp2Element);
            if (ret != 0)
                throw new ArgumentException("Failed to map Fp2 to G2");

            return EncodeG2(result);
        }

        private MclBindings.MclBnG1 DecodeG1(byte[] eip2537)
        {
            if (eip2537.Length != EIP2537_G1_POINT_SIZE)
                throw new ArgumentException($"G1 point must be {EIP2537_G1_POINT_SIZE} bytes");

            ValidateFpPadding(eip2537, 0);
            ValidateFpPadding(eip2537, EIP2537_FP_SIZE);

            bool isZero = IsAllZero(eip2537);
            if (isZero)
            {
                var zero = new MclBindings.MclBnG1();
                MclBindings.mclBnG1_clear(ref zero);
                return zero;
            }

            var mclBytes = ConvertEip2537G1ToMcl(eip2537);
            var point = new MclBindings.MclBnG1();
            ulong n = MclBindings.mclBnG1_deserialize(ref point, mclBytes, (ulong)mclBytes.Length);
            if (n == 0)
                throw new ArgumentException("Invalid G1 point: deserialization failed");

            if (MclBindings.mclBnG1_isValid(point) == 0)
                throw new ArgumentException("Invalid G1 point: not on curve or not in subgroup");

            return point;
        }

        private MclBindings.MclBnG2 DecodeG2(byte[] eip2537)
        {
            if (eip2537.Length != EIP2537_G2_POINT_SIZE)
                throw new ArgumentException($"G2 point must be {EIP2537_G2_POINT_SIZE} bytes");

            ValidateFpPadding(eip2537, 0);
            ValidateFpPadding(eip2537, EIP2537_FP_SIZE);
            ValidateFpPadding(eip2537, EIP2537_FP_SIZE * 2);
            ValidateFpPadding(eip2537, EIP2537_FP_SIZE * 3);

            bool isZero = IsAllZero(eip2537);
            if (isZero)
            {
                var zero = new MclBindings.MclBnG2();
                MclBindings.mclBnG2_clear(ref zero);
                return zero;
            }

            var mclBytes = ConvertEip2537G2ToMcl(eip2537);
            var point = new MclBindings.MclBnG2();
            ulong n = MclBindings.mclBnG2_deserialize(ref point, mclBytes, (ulong)mclBytes.Length);
            if (n == 0)
                throw new ArgumentException("Invalid G2 point: deserialization failed");

            if (MclBindings.mclBnG2_isValid(point) == 0)
                throw new ArgumentException("Invalid G2 point: not on curve or not in subgroup");

            return point;
        }

        private MclBindings.MclBnFr DecodeScalar(byte[] eip2537)
        {
            if (eip2537.Length != EIP2537_SCALAR_SIZE)
                throw new ArgumentException($"Scalar must be {EIP2537_SCALAR_SIZE} bytes");

            var mclBytes = new byte[EIP2537_SCALAR_SIZE];
            for (int i = 0; i < EIP2537_SCALAR_SIZE; i++)
                mclBytes[i] = eip2537[EIP2537_SCALAR_SIZE - 1 - i];

            var scalar = new MclBindings.MclBnFr();
            ulong n = MclBindings.mclBnFr_deserialize(ref scalar, mclBytes, (ulong)mclBytes.Length);
            if (n == 0)
                throw new ArgumentException("Invalid scalar");

            return scalar;
        }

        private MclBindings.MclBnFp DecodeFp(byte[] eip2537)
        {
            ValidateFpPadding(eip2537, 0);

            var mclBytes = new byte[MCL_FP_SIZE];
            for (int i = 0; i < MCL_FP_SIZE; i++)
                mclBytes[i] = eip2537[EIP2537_FP_SIZE - 1 - i];

            var fp = new MclBindings.MclBnFp();
            ulong n = MclBindings.mclBnFp_deserialize(ref fp, mclBytes, (ulong)mclBytes.Length);
            if (n == 0)
                throw new ArgumentException("Invalid Fp element");

            return fp;
        }

        private MclBindings.MclBnFp2 DecodeFp2(byte[] eip2537)
        {
            ValidateFpPadding(eip2537, 0);
            ValidateFpPadding(eip2537, EIP2537_FP_SIZE);

            var fp2 = new MclBindings.MclBnFp2();

            var c1Bytes = new byte[MCL_FP_SIZE];
            for (int i = 0; i < MCL_FP_SIZE; i++)
                c1Bytes[i] = eip2537[EIP2537_FP_SIZE - 1 - i];

            var c0Bytes = new byte[MCL_FP_SIZE];
            for (int i = 0; i < MCL_FP_SIZE; i++)
                c0Bytes[i] = eip2537[EIP2537_FP2_SIZE - 1 - i];

            ulong n1 = MclBindings.mclBnFp_deserialize(ref fp2.c1, c1Bytes, (ulong)c1Bytes.Length);
            ulong n0 = MclBindings.mclBnFp_deserialize(ref fp2.c0, c0Bytes, (ulong)c0Bytes.Length);

            if (n0 == 0 || n1 == 0)
                throw new ArgumentException("Invalid Fp2 element");

            return fp2;
        }

        private byte[] EncodeG1(MclBindings.MclBnG1 point)
        {
            if (MclBindings.mclBnG1_isZero(point) == 1)
                return new byte[EIP2537_G1_POINT_SIZE];

            var mclBytes = new byte[MCL_G1_SERIALIZED_SIZE * 2];
            ulong n = MclBindings.mclBnG1_serialize(mclBytes, (ulong)mclBytes.Length, point);
            if (n == 0)
                throw new InvalidOperationException("G1 serialization failed");

            return ConvertMclG1ToEip2537(mclBytes, (int)n);
        }

        private byte[] EncodeG2(MclBindings.MclBnG2 point)
        {
            if (MclBindings.mclBnG2_isZero(point) == 1)
                return new byte[EIP2537_G2_POINT_SIZE];

            var mclBytes = new byte[MCL_G2_SERIALIZED_SIZE * 2];
            ulong n = MclBindings.mclBnG2_serialize(mclBytes, (ulong)mclBytes.Length, point);
            if (n == 0)
                throw new InvalidOperationException("G2 serialization failed");

            return ConvertMclG2ToEip2537(mclBytes, (int)n);
        }

        private void ValidateFpPadding(byte[] data, int offset)
        {
            for (int i = 0; i < 16; i++)
            {
                if (data[offset + i] != 0)
                    throw new ArgumentException($"Invalid padding at offset {offset}: first 16 bytes must be zero");
            }
        }

        private bool IsAllZero(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0)
                    return false;
            }
            return true;
        }

        private byte[] ConvertEip2537G1ToMcl(byte[] eip2537)
        {
            var result = new byte[MCL_FP_SIZE * 2];

            for (int i = 0; i < MCL_FP_SIZE; i++)
                result[i] = eip2537[EIP2537_FP_SIZE - 1 - i];

            for (int i = 0; i < MCL_FP_SIZE; i++)
                result[MCL_FP_SIZE + i] = eip2537[EIP2537_G1_POINT_SIZE - 1 - i];

            return result;
        }

        private byte[] ConvertMclG1ToEip2537(byte[] mcl, int mclLen)
        {
            var result = new byte[EIP2537_G1_POINT_SIZE];

            if (mclLen == MCL_FP_SIZE)
            {
                throw new InvalidOperationException("Compressed G1 points not supported");
            }
            else if (mclLen == MCL_FP_SIZE * 2)
            {
                for (int i = 0; i < MCL_FP_SIZE; i++)
                    result[EIP2537_FP_SIZE - 1 - i] = mcl[i];

                for (int i = 0; i < MCL_FP_SIZE; i++)
                    result[EIP2537_G1_POINT_SIZE - 1 - i] = mcl[MCL_FP_SIZE + i];
            }
            else
            {
                throw new InvalidOperationException($"Unexpected MCL G1 serialization length: {mclLen}");
            }

            return result;
        }

        private byte[] ConvertEip2537G2ToMcl(byte[] eip2537)
        {
            var result = new byte[MCL_FP_SIZE * 4];

            for (int i = 0; i < MCL_FP_SIZE; i++)
                result[i] = eip2537[EIP2537_FP_SIZE - 1 - i];

            for (int i = 0; i < MCL_FP_SIZE; i++)
                result[MCL_FP_SIZE + i] = eip2537[EIP2537_FP2_SIZE - 1 - i];

            for (int i = 0; i < MCL_FP_SIZE; i++)
                result[MCL_FP_SIZE * 2 + i] = eip2537[EIP2537_FP_SIZE * 3 - 1 - i];

            for (int i = 0; i < MCL_FP_SIZE; i++)
                result[MCL_FP_SIZE * 3 + i] = eip2537[EIP2537_G2_POINT_SIZE - 1 - i];

            return result;
        }

        private byte[] ConvertMclG2ToEip2537(byte[] mcl, int mclLen)
        {
            var result = new byte[EIP2537_G2_POINT_SIZE];

            if (mclLen == MCL_FP_SIZE * 2)
            {
                throw new InvalidOperationException("Compressed G2 points not supported");
            }
            else if (mclLen == MCL_FP_SIZE * 4)
            {
                for (int i = 0; i < MCL_FP_SIZE; i++)
                    result[EIP2537_FP_SIZE - 1 - i] = mcl[i];

                for (int i = 0; i < MCL_FP_SIZE; i++)
                    result[EIP2537_FP2_SIZE - 1 - i] = mcl[MCL_FP_SIZE + i];

                for (int i = 0; i < MCL_FP_SIZE; i++)
                    result[EIP2537_FP_SIZE * 3 - 1 - i] = mcl[MCL_FP_SIZE * 2 + i];

                for (int i = 0; i < MCL_FP_SIZE; i++)
                    result[EIP2537_G2_POINT_SIZE - 1 - i] = mcl[MCL_FP_SIZE * 3 + i];
            }
            else
            {
                throw new InvalidOperationException($"Unexpected MCL G2 serialization length: {mclLen}");
            }

            return result;
        }
    }
}
