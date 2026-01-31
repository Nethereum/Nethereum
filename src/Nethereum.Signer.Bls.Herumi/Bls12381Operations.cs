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

        public Bls12381Operations()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }
            lock (InitLock)
            {
                if (!_initialized)
                {
                    BLS.Init();
                    MclBindings.mclBn_setETHserialization(0);
                    _initialized = true;
                }
            }
        }

        public byte[] G1Add(byte[] p1, byte[] p2)
        {
            MclBindings.MclBnG1 x = DecodeG1(p1);
            MclBindings.MclBnG1 y = DecodeG1(p2);
            MclBindings.MclBnG1 z = default(MclBindings.MclBnG1);
            MclBindings.mclBnG1_add(ref z, in x, in y);
            return EncodeG1(z);
        }

        public byte[] G1Mul(byte[] point, byte[] scalar)
        {
            MclBindings.MclBnG1 x = DecodeG1(point);
            MclBindings.MclBnFr y = DecodeScalar(scalar);
            MclBindings.MclBnG1 z = default(MclBindings.MclBnG1);
            MclBindings.mclBnG1_mul(ref z, in x, in y);
            return EncodeG1(z);
        }

        public byte[] G1Msm(byte[][] points, byte[][] scalars)
        {
            if (points.Length != scalars.Length)
            {
                throw new ArgumentException("Points and scalars must have same length");
            }
            if (points.Length == 0)
            {
                throw new ArgumentException("Empty input");
            }
            MclBindings.MclBnG1[] array = new MclBindings.MclBnG1[points.Length];
            MclBindings.MclBnFr[] array2 = new MclBindings.MclBnFr[scalars.Length];
            for (int i = 0; i < points.Length; i++)
            {
                array[i] = DecodeG1(points[i]);
                array2[i] = DecodeScalar(scalars[i]);
            }
            MclBindings.MclBnG1 z = default(MclBindings.MclBnG1);
            MclBindings.mclBnG1_mulVec(ref z, in array[0], in array2[0], (ulong)points.Length);
            return EncodeG1(z);
        }

        public byte[] G2Add(byte[] p1, byte[] p2)
        {
            MclBindings.MclBnG2 x = DecodeG2(p1);
            MclBindings.MclBnG2 y = DecodeG2(p2);
            MclBindings.MclBnG2 z = default(MclBindings.MclBnG2);
            MclBindings.mclBnG2_add(ref z, in x, in y);
            return EncodeG2(z);
        }

        public byte[] G2Mul(byte[] point, byte[] scalar)
        {
            MclBindings.MclBnG2 x = DecodeG2(point);
            MclBindings.MclBnFr y = DecodeScalar(scalar);
            MclBindings.MclBnG2 z = default(MclBindings.MclBnG2);
            MclBindings.mclBnG2_mul(ref z, in x, in y);
            return EncodeG2(z);
        }

        public byte[] G2Msm(byte[][] points, byte[][] scalars)
        {
            if (points.Length != scalars.Length)
            {
                throw new ArgumentException("Points and scalars must have same length");
            }
            if (points.Length == 0)
            {
                throw new ArgumentException("Empty input");
            }
            MclBindings.MclBnG2[] array = new MclBindings.MclBnG2[points.Length];
            MclBindings.MclBnFr[] array2 = new MclBindings.MclBnFr[scalars.Length];
            for (int i = 0; i < points.Length; i++)
            {
                array[i] = DecodeG2(points[i]);
                array2[i] = DecodeScalar(scalars[i]);
            }
            MclBindings.MclBnG2 z = default(MclBindings.MclBnG2);
            MclBindings.mclBnG2_mulVec(ref z, in array[0], in array2[0], (ulong)points.Length);
            return EncodeG2(z);
        }

        public bool Pairing(byte[][] g1Points, byte[][] g2Points)
        {
            if (g1Points.Length != g2Points.Length)
            {
                throw new ArgumentException("G1 and G2 point arrays must have same length");
            }
            if (g1Points.Length == 0)
            {
                return true;
            }
            MclBindings.MclBnG1[] array = new MclBindings.MclBnG1[g1Points.Length];
            MclBindings.MclBnG2[] array2 = new MclBindings.MclBnG2[g2Points.Length];
            for (int i = 0; i < g1Points.Length; i++)
            {
                array[i] = DecodeG1(g1Points[i]);
                array2[i] = DecodeG2(g2Points[i]);
            }
            MclBindings.MclBnGT z = default(MclBindings.MclBnGT);
            MclBindings.mclBn_millerLoopVec(ref z, in array[0], in array2[0], (ulong)g1Points.Length);
            MclBindings.mclBn_finalExp(ref z, in z);
            return MclBindings.mclBnGT_isOne(in z) == 1;
        }

        public byte[] MapFpToG1(byte[] fp)
        {
            if (fp.Length != 64)
            {
                throw new ArgumentException($"Invalid Fp length: expected {64}, got {fp.Length}");
            }
            MclBindings.MclBnFp x = DecodeFp(fp);
            MclBindings.MclBnG1 y = default(MclBindings.MclBnG1);
            if (MclBindings.mclBnFp_mapToG1(ref y, in x) != 0)
            {
                throw new ArgumentException("Failed to map Fp to G1");
            }
            return EncodeG1(y);
        }

        public byte[] MapFp2ToG2(byte[] fp2)
        {
            if (fp2.Length != 128)
            {
                throw new ArgumentException($"Invalid Fp2 length: expected {128}, got {fp2.Length}");
            }
            MclBindings.MclBnFp2 x = DecodeFp2(fp2);
            MclBindings.MclBnG2 y = default(MclBindings.MclBnG2);
            if (MclBindings.mclBnFp2_mapToG2(ref y, in x) != 0)
            {
                throw new ArgumentException("Failed to map Fp2 to G2");
            }
            return EncodeG2(y);
        }

        private MclBindings.MclBnG1 DecodeG1(byte[] eip2537)
        {
            if (eip2537.Length != 128)
            {
                throw new ArgumentException($"G1 point must be {128} bytes");
            }
            ValidateFpPadding(eip2537, 0);
            ValidateFpPadding(eip2537, 64);
            if (IsAllZero(eip2537))
            {
                MclBindings.MclBnG1 x = default(MclBindings.MclBnG1);
                MclBindings.mclBnG1_clear(ref x);
                return x;
            }
            MclBindings.MclBnG1 x2 = default(MclBindings.MclBnG1);
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            if (MclBindings.mclBnFp_deserialize(ref x2.x, array, (ulong)array.Length) == 0L)
            {
                throw new ArgumentException("Invalid G1 point: failed to deserialize x coordinate");
            }
            byte[] array2 = ExtractFpAsLittleEndian(eip2537, 64);
            if (MclBindings.mclBnFp_deserialize(ref x2.y, array2, (ulong)array2.Length) == 0L)
            {
                throw new ArgumentException("Invalid G1 point: failed to deserialize y coordinate");
            }
            MclBindings.mclBnFp_setInt(ref x2.z, 1);
            if (MclBindings.mclBnG1_isValid(in x2) == 0)
            {
                throw new ArgumentException("Invalid G1 point: not on curve or not in subgroup");
            }
            return x2;
        }

        private MclBindings.MclBnG2 DecodeG2(byte[] eip2537)
        {
            if (eip2537.Length != 256)
            {
                throw new ArgumentException($"G2 point must be {256} bytes");
            }
            ValidateFpPadding(eip2537, 0);
            ValidateFpPadding(eip2537, 64);
            ValidateFpPadding(eip2537, 128);
            ValidateFpPadding(eip2537, 192);
            if (IsAllZero(eip2537))
            {
                MclBindings.MclBnG2 x = default(MclBindings.MclBnG2);
                MclBindings.mclBnG2_clear(ref x);
                return x;
            }
            MclBindings.MclBnG2 x2 = default(MclBindings.MclBnG2);
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            byte[] array2 = ExtractFpAsLittleEndian(eip2537, 64);
            byte[] array3 = ExtractFpAsLittleEndian(eip2537, 128);
            byte[] array4 = ExtractFpAsLittleEndian(eip2537, 192);
            ulong num = MclBindings.mclBnFp_deserialize(ref x2.x.c0, array, (ulong)array.Length);
            ulong num2 = MclBindings.mclBnFp_deserialize(ref x2.x.c1, array2, (ulong)array2.Length);
            ulong num3 = MclBindings.mclBnFp_deserialize(ref x2.y.c0, array3, (ulong)array3.Length);
            ulong num4 = MclBindings.mclBnFp_deserialize(ref x2.y.c1, array4, (ulong)array4.Length);
            if (num == 0L || num2 == 0L || num3 == 0L || num4 == 0L)
            {
                throw new ArgumentException("Invalid G2 point: failed to deserialize coordinates");
            }
            MclBindings.mclBnFp_setInt(ref x2.z.c0, 1);
            MclBindings.mclBnFp_setInt(ref x2.z.c1, 0);
            if (MclBindings.mclBnG2_isValid(in x2) == 0)
            {
                throw new ArgumentException("Invalid G2 point: not on curve or not in subgroup");
            }
            return x2;
        }

        private MclBindings.MclBnFr DecodeScalar(byte[] eip2537)
        {
            if (eip2537.Length != 32)
            {
                throw new ArgumentException($"Scalar must be {32} bytes");
            }
            byte[] array = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                array[i] = eip2537[31 - i];
            }
            MclBindings.MclBnFr x = default(MclBindings.MclBnFr);
            if (MclBindings.mclBnFr_deserialize(ref x, array, (ulong)array.Length) == 0L)
            {
                throw new ArgumentException("Invalid scalar");
            }
            return x;
        }

        private MclBindings.MclBnFp DecodeFp(byte[] eip2537)
        {
            ValidateFpPadding(eip2537, 0);
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            MclBindings.MclBnFp x = default(MclBindings.MclBnFp);
            if (MclBindings.mclBnFp_deserialize(ref x, array, (ulong)array.Length) == 0L)
            {
                throw new ArgumentException("Invalid Fp element");
            }
            return x;
        }

        private MclBindings.MclBnFp2 DecodeFp2(byte[] eip2537)
        {
            ValidateFpPadding(eip2537, 0);
            ValidateFpPadding(eip2537, 64);
            MclBindings.MclBnFp2 result = default(MclBindings.MclBnFp2);
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            byte[] array2 = ExtractFpAsLittleEndian(eip2537, 64);
            ulong num = MclBindings.mclBnFp_deserialize(ref result.c0, array, (ulong)array.Length);
            ulong num2 = MclBindings.mclBnFp_deserialize(ref result.c1, array2, (ulong)array2.Length);
            if (num == 0L || num2 == 0L)
            {
                throw new ArgumentException("Invalid Fp2 element");
            }
            return result;
        }

        private byte[] EncodeG1(MclBindings.MclBnG1 point)
        {
            if (MclBindings.mclBnG1_isZero(in point) == 1)
            {
                return new byte[128];
            }
            MclBindings.MclBnG1 y = default(MclBindings.MclBnG1);
            MclBindings.mclBnG1_normalize(ref y, in point);
            byte[] array = new byte[128];
            byte[] array2 = new byte[48];
            if (MclBindings.mclBnFp_serialize(array2, (ulong)array2.Length, in y.x) == 0L)
            {
                throw new InvalidOperationException("G1 encoding failed: could not serialize x coordinate");
            }
            WriteFpAsBigEndian(array, 0, array2);
            byte[] array3 = new byte[48];
            if (MclBindings.mclBnFp_serialize(array3, (ulong)array3.Length, in y.y) == 0L)
            {
                throw new InvalidOperationException("G1 encoding failed: could not serialize y coordinate");
            }
            WriteFpAsBigEndian(array, 64, array3);
            return array;
        }

        private byte[] EncodeG2(MclBindings.MclBnG2 point)
        {
            if (MclBindings.mclBnG2_isZero(in point) == 1)
            {
                return new byte[256];
            }
            MclBindings.MclBnG2 y = default(MclBindings.MclBnG2);
            MclBindings.mclBnG2_normalize(ref y, in point);
            byte[] array = new byte[256];
            byte[] array2 = new byte[48];
            byte[] array3 = new byte[48];
            byte[] array4 = new byte[48];
            byte[] array5 = new byte[48];
            ulong num = MclBindings.mclBnFp_serialize(array2, (ulong)array2.Length, in y.x.c0);
            ulong num2 = MclBindings.mclBnFp_serialize(array3, (ulong)array3.Length, in y.x.c1);
            ulong num3 = MclBindings.mclBnFp_serialize(array4, (ulong)array4.Length, in y.y.c0);
            ulong num4 = MclBindings.mclBnFp_serialize(array5, (ulong)array5.Length, in y.y.c1);
            if (num == 0L || num2 == 0L || num3 == 0L || num4 == 0L)
            {
                throw new InvalidOperationException("G2 encoding failed: could not serialize coordinates");
            }
            WriteFpAsBigEndian(array, 0, array2);
            WriteFpAsBigEndian(array, 64, array3);
            WriteFpAsBigEndian(array, 128, array4);
            WriteFpAsBigEndian(array, 192, array5);
            return array;
        }

        private byte[] ExtractFpAsLittleEndian(byte[] eip2537, int offset)
        {
            byte[] array = new byte[48];
            for (int i = 0; i < 48; i++)
            {
                array[i] = eip2537[offset + 16 + 48 - 1 - i];
            }
            return array;
        }

        private void WriteFpAsBigEndian(byte[] dest, int offset, byte[] littleEndian)
        {
            for (int i = 0; i < 48; i++)
            {
                dest[offset + 16 + 48 - 1 - i] = littleEndian[i];
            }
        }

        private void ValidateFpPadding(byte[] data, int offset)
        {
            for (int i = 0; i < 16; i++)
            {
                if (data[offset + i] != 0)
                {
                    throw new ArgumentException($"Invalid padding at offset {offset}: first 16 bytes must be zero");
                }
            }
        }

        private bool IsAllZero(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
