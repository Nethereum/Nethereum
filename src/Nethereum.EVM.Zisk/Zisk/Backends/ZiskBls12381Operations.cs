using System;
using Nethereum.Signer.Bls;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskBls12381Operations : IBls12381Operations
    {
        public static readonly ZiskBls12381Operations Instance = new ZiskBls12381Operations();

        private const int EipG1 = 128;
        private const int EipG2 = 256;
        private const int EipFp = 64;
        private const int EipFp2 = 128;
        private const int Scalar = 32;

        private const int RawG1 = 96;
        private const int RawG2 = 192;
        private const int RawFp = 48;
        private const int RawFp2 = 96;

        private const int RawG1MsmPair = RawG1 + Scalar;
        private const int RawG2MsmPair = RawG2 + Scalar;
        private const int RawPairingPair = RawG1 + RawG2;

        public byte[] G1Add(byte[] p1, byte[] p2)
        {
            var a = StripG1(p1, "G1ADD p1");
            var b = StripG1(p2, "G1ADD p2");
            var rawRet = new byte[RawG1];
            uint status = ZiskCrypto.zkvm_bls12_g1_add(a, b, rawRet);
            if (status != 0)
                throw new ArgumentException($"BLS12-381 G1ADD failed (status {status})");
            return PadG1(rawRet);
        }

        public byte[] G1Mul(byte[] point, byte[] scalar)
            => throw new NotImplementedException(
                "ZiskBls12381Operations.G1Mul is not available.");

        public byte[] G1Msm(byte[][] points, byte[][] scalars)
        {
            if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0)
                throw new ArgumentException("BLS12-381 G1MSM: points and scalars must be non-empty and equal length");

            int k = points.Length;
            var pairs = new byte[k * RawG1MsmPair];
            for (int i = 0; i < k; i++)
            {
                var rawPoint = StripG1(points[i], $"G1MSM point[{i}]");
                Array.Copy(rawPoint, 0, pairs, i * RawG1MsmPair, RawG1);
                if (scalars[i] == null || scalars[i].Length != Scalar)
                    throw new ArgumentException($"BLS12-381 G1MSM: scalar[{i}] must be {Scalar} bytes");
                Array.Copy(scalars[i], 0, pairs, i * RawG1MsmPair + RawG1, Scalar);
            }

            var rawRet = new byte[RawG1];
            uint status = ZiskCrypto.zkvm_bls12_g1_msm(pairs, (nuint)k, rawRet);
            if (status != 0)
                throw new ArgumentException($"BLS12-381 G1MSM failed (status {status})");
            return PadG1(rawRet);
        }

        public byte[] G2Add(byte[] p1, byte[] p2)
        {
            var a = StripG2(p1, "G2ADD p1");
            var b = StripG2(p2, "G2ADD p2");
            var rawRet = new byte[RawG2];
            uint status = ZiskCrypto.zkvm_bls12_g2_add(a, b, rawRet);
            if (status != 0)
                throw new ArgumentException($"BLS12-381 G2ADD failed (status {status})");
            return PadG2(rawRet);
        }

        public byte[] G2Mul(byte[] point, byte[] scalar)
            => throw new NotImplementedException(
                "ZiskBls12381Operations.G2Mul is not available.");

        public byte[] G2Msm(byte[][] points, byte[][] scalars)
        {
            if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0)
                throw new ArgumentException("BLS12-381 G2MSM: points and scalars must be non-empty and equal length");

            int k = points.Length;
            var pairs = new byte[k * RawG2MsmPair];
            for (int i = 0; i < k; i++)
            {
                var rawPoint = StripG2(points[i], $"G2MSM point[{i}]");
                Array.Copy(rawPoint, 0, pairs, i * RawG2MsmPair, RawG2);
                if (scalars[i] == null || scalars[i].Length != Scalar)
                    throw new ArgumentException($"BLS12-381 G2MSM: scalar[{i}] must be {Scalar} bytes");
                Array.Copy(scalars[i], 0, pairs, i * RawG2MsmPair + RawG2, Scalar);
            }

            var rawRet = new byte[RawG2];
            uint status = ZiskCrypto.zkvm_bls12_g2_msm(pairs, (nuint)k, rawRet);
            if (status != 0)
                throw new ArgumentException($"BLS12-381 G2MSM failed (status {status})");
            return PadG2(rawRet);
        }

        public bool Pairing(byte[][] g1Points, byte[][] g2Points)
        {
            if (g1Points == null || g2Points == null || g1Points.Length != g2Points.Length || g1Points.Length == 0)
                throw new ArgumentException("BLS12-381 pairing: g1/g2 arrays must be non-empty and equal length");

            int k = g1Points.Length;
            var pairs = new byte[k * RawPairingPair];
            for (int i = 0; i < k; i++)
            {
                var rawG1 = StripG1(g1Points[i], $"pairing g1[{i}]");
                Array.Copy(rawG1, 0, pairs, i * RawPairingPair, RawG1);
                var rawG2 = StripG2(g2Points[i], $"pairing g2[{i}]");
                Array.Copy(rawG2, 0, pairs, i * RawPairingPair + RawG1, RawG2);
            }

            byte result = ZiskCrypto.bls12_381_pairing_check_c(pairs, (nuint)k);
            return result == 0;
        }

        public byte[] MapFpToG1(byte[] fp)
        {
            var rawFp = StripFp(fp, 0, "MAP_FP_TO_G1");
            var rawRet = new byte[RawG1];
            uint status = ZiskCrypto.zkvm_bls12_map_fp_to_g1(rawFp, rawRet);
            if (status != 0)
                throw new ArgumentException($"BLS12-381 MAP_FP_TO_G1 failed (status {status})");
            return PadG1(rawRet);
        }

        public byte[] MapFp2ToG2(byte[] fp2)
        {
            if (fp2 == null || fp2.Length != EipFp2)
                throw new ArgumentException($"BLS12-381 MAP_FP2_TO_G2: input must be {EipFp2} bytes");
            var rawFp2 = new byte[RawFp2];
            StripFpInto(fp2, 0, rawFp2, 0, "MAP_FP2_TO_G2 c0");
            StripFpInto(fp2, EipFp, rawFp2, RawFp, "MAP_FP2_TO_G2 c1");

            var rawRet = new byte[RawG2];
            uint status = ZiskCrypto.zkvm_bls12_map_fp2_to_g2(rawFp2, rawRet);
            if (status != 0)
                throw new ArgumentException($"BLS12-381 MAP_FP2_TO_G2 failed (status {status})");
            return PadG2(rawRet);
        }

        private static byte[] StripG1(byte[] eipG1, string where)
        {
            if (eipG1 == null || eipG1.Length != EipG1)
                throw new ArgumentException($"BLS12-381 {where}: point must be {EipG1} bytes");
            var raw = new byte[RawG1];
            StripFpInto(eipG1, 0, raw, 0, where + " x");
            StripFpInto(eipG1, EipFp, raw, RawFp, where + " y");
            return raw;
        }

        private static byte[] PadG1(byte[] rawG1)
        {
            var padded = new byte[EipG1];
            PadFpInto(rawG1, 0, padded, 0);
            PadFpInto(rawG1, RawFp, padded, EipFp);
            return padded;
        }

        private static byte[] StripG2(byte[] eipG2, string where)
        {
            if (eipG2 == null || eipG2.Length != EipG2)
                throw new ArgumentException($"BLS12-381 {where}: point must be {EipG2} bytes");
            var raw = new byte[RawG2];
            StripFpInto(eipG2, 0 * EipFp, raw, 0 * RawFp, where + " x.c0");
            StripFpInto(eipG2, 1 * EipFp, raw, 1 * RawFp, where + " x.c1");
            StripFpInto(eipG2, 2 * EipFp, raw, 2 * RawFp, where + " y.c0");
            StripFpInto(eipG2, 3 * EipFp, raw, 3 * RawFp, where + " y.c1");
            return raw;
        }

        private static byte[] PadG2(byte[] rawG2)
        {
            var padded = new byte[EipG2];
            PadFpInto(rawG2, 0 * RawFp, padded, 0 * EipFp);
            PadFpInto(rawG2, 1 * RawFp, padded, 1 * EipFp);
            PadFpInto(rawG2, 2 * RawFp, padded, 2 * EipFp);
            PadFpInto(rawG2, 3 * RawFp, padded, 3 * EipFp);
            return padded;
        }

        private static byte[] StripFp(byte[] eipFp, int offset, string where)
        {
            if (eipFp == null || eipFp.Length - offset < EipFp)
                throw new ArgumentException($"BLS12-381 {where}: Fp must be {EipFp} bytes");
            var raw = new byte[RawFp];
            StripFpInto(eipFp, offset, raw, 0, where);
            return raw;
        }

        private static void StripFpInto(byte[] src, int srcOff, byte[] dst, int dstOff, string where)
        {
            for (int i = 0; i < 16; i++)
            {
                if (src[srcOff + i] != 0)
                    throw new ArgumentException($"BLS12-381 {where}: invalid Fp encoding (non-zero padding byte at index {i})");
            }
            Array.Copy(src, srcOff + 16, dst, dstOff, RawFp);
        }

        private static void PadFpInto(byte[] rawSrc, int srcOff, byte[] paddedDst, int dstOff)
        {
            for (int i = 0; i < 16; i++) paddedDst[dstOff + i] = 0;
            Array.Copy(rawSrc, srcOff, paddedDst, dstOff + 16, RawFp);
        }
    }
}
