using System;
using mcl;

namespace Nethereum.Signer.Bls.Herumi
{
    public class Bls12381Operations : IBls12381Operations, IBls12381OutcomeOperations
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
                    _initialized = true;
                }
            }
        }

        public byte[] G1Add(byte[] p1, byte[] p2)
            => TryG1Add(p1, p2, out var result) ? result : throw new ArgumentException("Invalid G1ADD input");

        public byte[] G1Mul(byte[] point, byte[] scalar)
            => TryG1Mul(point, scalar, out var result) ? result : throw new ArgumentException("Invalid G1MUL input");

        public byte[] G1Msm(byte[][] points, byte[][] scalars)
            => TryG1Msm(points, scalars, out var result) ? result : throw new ArgumentException("Invalid G1MSM input");

        public byte[] G2Add(byte[] p1, byte[] p2)
            => TryG2Add(p1, p2, out var result) ? result : throw new ArgumentException("Invalid G2ADD input");

        public byte[] G2Mul(byte[] point, byte[] scalar)
            => TryG2Mul(point, scalar, out var result) ? result : throw new ArgumentException("Invalid G2MUL input");

        public byte[] G2Msm(byte[][] points, byte[][] scalars)
            => TryG2Msm(points, scalars, out var result) ? result : throw new ArgumentException("Invalid G2MSM input");

        public bool Pairing(byte[][] g1Points, byte[][] g2Points)
            => TryPairing(g1Points, g2Points, out var pairs) ? pairs : throw new ArgumentException("Invalid PAIRING input");

        public byte[] MapFpToG1(byte[] fp)
            => TryMapFpToG1(fp, out var result) ? result : throw new ArgumentException("Invalid MAP_FP_TO_G1 input");

        public byte[] MapFp2ToG2(byte[] fp2)
            => TryMapFp2ToG2(fp2, out var result) ? result : throw new ArgumentException("Invalid MAP_FP2_TO_G2 input");

        public bool TryG1Add(byte[] p1, byte[] p2, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (!TryDecodeG1(p1, false, out var x) || !TryDecodeG1(p2, false, out var y)) return false;
                MclBindings.MclBnG1 z = default(MclBindings.MclBnG1);
                MclBindings.mclBnG1_add(ref z, in x, in y);
                encoded = EncodeG1(z);
                return true;
            });
            result = encoded;
            return ok;
        }

        public bool TryG1Mul(byte[] point, byte[] scalar, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (!TryDecodeG1(point, true, out var x) || !TryDecodeScalar(scalar, out var y)) return false;
                MclBindings.MclBnG1 z = default(MclBindings.MclBnG1);
                MclBindings.mclBnG1_mul(ref z, in x, in y);
                encoded = EncodeG1(z);
                return true;
            });
            result = encoded;
            return ok;
        }

        public bool TryG1Msm(byte[][] points, byte[][] scalars, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0) return false;
                MclBindings.MclBnG1[] array = new MclBindings.MclBnG1[points.Length];
                MclBindings.MclBnFr[] array2 = new MclBindings.MclBnFr[scalars.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    if (!TryDecodeG1(points[i], true, out array[i])) return false;
                    if (!TryDecodeScalar(scalars[i], out array2[i])) return false;
                }
                MclBindings.MclBnG1 z = default(MclBindings.MclBnG1);
                MclBindings.mclBnG1_mulVec(ref z, in array[0], in array2[0], (ulong)points.Length);
                encoded = EncodeG1(z);
                return true;
            });
            result = encoded;
            return ok;
        }

        public bool TryG2Add(byte[] p1, byte[] p2, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (!TryDecodeG2(p1, false, out var x) || !TryDecodeG2(p2, false, out var y)) return false;
                MclBindings.MclBnG2 z = default(MclBindings.MclBnG2);
                MclBindings.mclBnG2_add(ref z, in x, in y);
                encoded = EncodeG2(z);
                return true;
            });
            result = encoded;
            return ok;
        }

        public bool TryG2Mul(byte[] point, byte[] scalar, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (!TryDecodeG2(point, true, out var x) || !TryDecodeScalar(scalar, out var y)) return false;
                MclBindings.MclBnG2 z = default(MclBindings.MclBnG2);
                MclBindings.mclBnG2_mul(ref z, in x, in y);
                encoded = EncodeG2(z);
                return true;
            });
            result = encoded;
            return ok;
        }

        public bool TryG2Msm(byte[][] points, byte[][] scalars, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0) return false;
                MclBindings.MclBnG2[] array = new MclBindings.MclBnG2[points.Length];
                MclBindings.MclBnFr[] array2 = new MclBindings.MclBnFr[scalars.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    if (!TryDecodeG2(points[i], true, out array[i])) return false;
                    if (!TryDecodeScalar(scalars[i], out array2[i])) return false;
                }
                MclBindings.MclBnG2 z = default(MclBindings.MclBnG2);
                MclBindings.mclBnG2_mulVec(ref z, in array[0], in array2[0], (ulong)points.Length);
                encoded = EncodeG2(z);
                return true;
            });
            result = encoded;
            return ok;
        }

        public bool TryPairing(byte[][] g1Points, byte[][] g2Points, out bool pairs)
        {
            bool verified = false;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (g1Points == null || g2Points == null || g1Points.Length != g2Points.Length) return false;
                if (g1Points.Length == 0)
                {
                    verified = true;
                    return true;
                }
                MclBindings.MclBnG1[] array = new MclBindings.MclBnG1[g1Points.Length];
                MclBindings.MclBnG2[] array2 = new MclBindings.MclBnG2[g2Points.Length];
                for (int i = 0; i < g1Points.Length; i++)
                {
                    if (!TryDecodeG1(g1Points[i], true, out array[i])) return false;
                    if (!TryDecodeG2(g2Points[i], true, out array2[i])) return false;
                }
                MclBindings.MclBnGT z = default(MclBindings.MclBnGT);
                MclBindings.mclBn_millerLoopVec(ref z, in array[0], in array2[0], (ulong)g1Points.Length);
                MclBindings.mclBn_finalExp(ref z, in z);
                verified = MclBindings.mclBnGT_isOne(in z) == 1;
                return true;
            });
            pairs = verified;
            return ok;
        }

        public bool TryMapFpToG1(byte[] fp, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (fp == null || fp.Length != EIP2537_FP_SIZE) return false;
                if (!TryDecodeFp(fp, out var x)) return false;
                MclBindings.MclBnG1 y = default(MclBindings.MclBnG1);
                if (MclBindings.mclBnFp_mapToG1(ref y, in x) != 0) return false;
                encoded = EncodeG1(y);
                return true;
            });
            result = encoded;
            return ok;
        }

        public bool TryMapFp2ToG2(byte[] fp2, out byte[] result)
        {
            byte[] encoded = null;
            var ok = MclSerialization.InEvmMode(() =>
            {
                if (fp2 == null || fp2.Length != EIP2537_FP2_SIZE) return false;
                if (!TryDecodeFp2(fp2, out var x)) return false;
                MclBindings.MclBnG2 y = default(MclBindings.MclBnG2);
                if (MclBindings.mclBnFp2_mapToG2(ref y, in x) != 0) return false;
                encoded = EncodeG2(y);
                return true;
            });
            result = encoded;
            return ok;
        }

        private bool TryDecodeG1(byte[] eip2537, bool requireSubgroup, out MclBindings.MclBnG1 point)
        {
            point = default(MclBindings.MclBnG1);
            if (eip2537 == null || eip2537.Length != EIP2537_G1_POINT_SIZE) return false;
            if (!HasValidFpPadding(eip2537, 0) || !HasValidFpPadding(eip2537, 64)) return false;
            if (IsAllZero(eip2537))
            {
                MclBindings.mclBnG1_clear(ref point);
                return true;
            }
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            byte[] array2 = ExtractFpAsLittleEndian(eip2537, 64);
            ulong xOk = MclBindings.mclBnFp_deserialize(ref point.x, array, (ulong)array.Length);
            ulong yOk = MclBindings.mclBnFp_deserialize(ref point.y, array2, (ulong)array2.Length);
            if (xOk == 0L || yOk == 0L) return false;
            MclBindings.mclBnFp_setInt(ref point.z, 1);
            if (MclBindings.mclBnG1_isValid(in point) == 0) return false;
            if (requireSubgroup && MclBindings.mclBnG1_isValidOrder(in point) != 1) return false;
            return true;
        }

        private bool TryDecodeG2(byte[] eip2537, bool requireSubgroup, out MclBindings.MclBnG2 point)
        {
            point = default(MclBindings.MclBnG2);
            if (eip2537 == null || eip2537.Length != EIP2537_G2_POINT_SIZE) return false;
            if (!HasValidFpPadding(eip2537, 0) || !HasValidFpPadding(eip2537, 64) ||
                !HasValidFpPadding(eip2537, 128) || !HasValidFpPadding(eip2537, 192)) return false;
            if (IsAllZero(eip2537))
            {
                MclBindings.mclBnG2_clear(ref point);
                return true;
            }
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            byte[] array2 = ExtractFpAsLittleEndian(eip2537, 64);
            byte[] array3 = ExtractFpAsLittleEndian(eip2537, 128);
            byte[] array4 = ExtractFpAsLittleEndian(eip2537, 192);
            ulong num = MclBindings.mclBnFp_deserialize(ref point.x.c0, array, (ulong)array.Length);
            ulong num2 = MclBindings.mclBnFp_deserialize(ref point.x.c1, array2, (ulong)array2.Length);
            ulong num3 = MclBindings.mclBnFp_deserialize(ref point.y.c0, array3, (ulong)array3.Length);
            ulong num4 = MclBindings.mclBnFp_deserialize(ref point.y.c1, array4, (ulong)array4.Length);
            if (num == 0L || num2 == 0L || num3 == 0L || num4 == 0L) return false;
            MclBindings.mclBnFp_setInt(ref point.z.c0, 1);
            MclBindings.mclBnFp_setInt(ref point.z.c1, 0);
            if (MclBindings.mclBnG2_isValid(in point) == 0) return false;
            if (requireSubgroup && MclBindings.mclBnG2_isValidOrder(in point) != 1) return false;
            return true;
        }

        private bool TryDecodeScalar(byte[] eip2537, out MclBindings.MclBnFr scalar)
        {
            scalar = default(MclBindings.MclBnFr);
            if (eip2537 == null || eip2537.Length != EIP2537_SCALAR_SIZE) return false;
            byte[] array = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                array[i] = eip2537[31 - i];
            }
            return MclBindings.mclBnFr_setLittleEndianMod(ref scalar, array, (ulong)array.Length) == 0;
        }

        private bool TryDecodeFp(byte[] eip2537, out MclBindings.MclBnFp element)
        {
            element = default(MclBindings.MclBnFp);
            if (!HasValidFpPadding(eip2537, 0)) return false;
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            return MclBindings.mclBnFp_deserialize(ref element, array, (ulong)array.Length) != 0L;
        }

        private bool TryDecodeFp2(byte[] eip2537, out MclBindings.MclBnFp2 element)
        {
            element = default(MclBindings.MclBnFp2);
            if (!HasValidFpPadding(eip2537, 0) || !HasValidFpPadding(eip2537, 64)) return false;
            byte[] array = ExtractFpAsLittleEndian(eip2537, 0);
            byte[] array2 = ExtractFpAsLittleEndian(eip2537, 64);
            ulong num = MclBindings.mclBnFp_deserialize(ref element.c0, array, (ulong)array.Length);
            ulong num2 = MclBindings.mclBnFp_deserialize(ref element.c1, array2, (ulong)array2.Length);
            return num != 0L && num2 != 0L;
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

        private bool HasValidFpPadding(byte[] data, int offset)
        {
            if (data == null || data.Length < offset + 16) return false;
            for (int i = 0; i < 16; i++)
            {
                if (data[offset + i] != 0) return false;
            }
            return true;
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
