using System;
using Nethereum.Signer.Bls;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskBls12381Operations : IBls12381Operations, IBls12381OutcomeOperations
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
            => TryG1Add(p1, p2, out var result) ? result : throw new ArgumentException("BLS12-381 G1ADD: invalid input");

        public byte[] G1Mul(byte[] point, byte[] scalar)
            => throw new NotImplementedException(
                "ZiskBls12381Operations.G1Mul is not available.");

        public byte[] G1Msm(byte[][] points, byte[][] scalars)
            => TryG1Msm(points, scalars, out var result) ? result : throw new ArgumentException("BLS12-381 G1MSM: invalid input");

        public byte[] G2Add(byte[] p1, byte[] p2)
            => TryG2Add(p1, p2, out var result) ? result : throw new ArgumentException("BLS12-381 G2ADD: invalid input");

        public byte[] G2Mul(byte[] point, byte[] scalar)
            => throw new NotImplementedException(
                "ZiskBls12381Operations.G2Mul is not available.");

        public byte[] G2Msm(byte[][] points, byte[][] scalars)
            => TryG2Msm(points, scalars, out var result) ? result : throw new ArgumentException("BLS12-381 G2MSM: invalid input");

        public bool Pairing(byte[][] g1Points, byte[][] g2Points)
            => TryPairing(g1Points, g2Points, out var pairs) ? pairs : throw new ArgumentException("BLS12-381 PAIRING: invalid input");

        public byte[] MapFpToG1(byte[] fp)
            => TryMapFpToG1(fp, out var result) ? result : throw new ArgumentException("BLS12-381 MAP_FP_TO_G1: invalid input");

        public byte[] MapFp2ToG2(byte[] fp2)
            => TryMapFp2ToG2(fp2, out var result) ? result : throw new ArgumentException("BLS12-381 MAP_FP2_TO_G2: invalid input");

        public bool TryG1Add(byte[] p1, byte[] p2, out byte[] result)
        {
            result = null;
            if (!TryStripG1(p1, out var a) || !TryStripG1(p2, out var b)) return false;
            var rawRet = new byte[RawG1];
            if (ZiskCrypto.zkvm_bls12_g1_add(a, b, rawRet) != 0) return false;
            result = PadG1(rawRet);
            return true;
        }

        public bool TryG1Msm(byte[][] points, byte[][] scalars, out byte[] result)
        {
            result = null;
            if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0) return false;

            int k = points.Length;
            var pairs = new byte[k * RawG1MsmPair];
            for (int i = 0; i < k; i++)
            {
                if (!TryStripG1(points[i], out var rawPoint)) return false;
                Array.Copy(rawPoint, 0, pairs, i * RawG1MsmPair, RawG1);
                if (scalars[i] == null || scalars[i].Length != Scalar) return false;
                Array.Copy(scalars[i], 0, pairs, i * RawG1MsmPair + RawG1, Scalar);
            }

            var rawRet = new byte[RawG1];
            if (ZiskCrypto.zkvm_bls12_g1_msm(pairs, (nuint)k, rawRet) != 0) return false;
            result = PadG1(rawRet);
            return true;
        }

        public bool TryG2Add(byte[] p1, byte[] p2, out byte[] result)
        {
            result = null;
            if (!TryStripG2(p1, out var a) || !TryStripG2(p2, out var b)) return false;
            var rawRet = new byte[RawG2];
            if (ZiskCrypto.zkvm_bls12_g2_add(a, b, rawRet) != 0) return false;
            result = PadG2(rawRet);
            return true;
        }

        public bool TryG2Msm(byte[][] points, byte[][] scalars, out byte[] result)
        {
            result = null;
            if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0) return false;

            int k = points.Length;
            var pairs = new byte[k * RawG2MsmPair];
            for (int i = 0; i < k; i++)
            {
                if (!TryStripG2(points[i], out var rawPoint)) return false;
                Array.Copy(rawPoint, 0, pairs, i * RawG2MsmPair, RawG2);
                if (scalars[i] == null || scalars[i].Length != Scalar) return false;
                Array.Copy(scalars[i], 0, pairs, i * RawG2MsmPair + RawG2, Scalar);
            }

            var rawRet = new byte[RawG2];
            if (ZiskCrypto.zkvm_bls12_g2_msm(pairs, (nuint)k, rawRet) != 0) return false;
            result = PadG2(rawRet);
            return true;
        }

        public bool TryPairing(byte[][] g1Points, byte[][] g2Points, out bool pairs)
        {
            pairs = false;
            if (g1Points == null || g2Points == null || g1Points.Length != g2Points.Length || g1Points.Length == 0) return false;

            int k = g1Points.Length;
            var raw = new byte[k * RawPairingPair];
            for (int i = 0; i < k; i++)
            {
                if (!TryStripG1(g1Points[i], out var rawG1)) return false;
                Array.Copy(rawG1, 0, raw, i * RawPairingPair, RawG1);
                if (!TryStripG2(g2Points[i], out var rawG2)) return false;
                Array.Copy(rawG2, 0, raw, i * RawPairingPair + RawG1, RawG2);
            }

            bool verified = false;
            uint status;
            unsafe { status = ZiskCrypto.zkvm_bls12_pairing(raw, (nuint)k, &verified); }
            if (status != 0) return false;
            pairs = verified;
            return true;
        }

        public bool TryMapFpToG1(byte[] fp, out byte[] result)
        {
            result = null;
            if (fp == null || fp.Length != EipFp) return false;
            var rawFp = new byte[RawFp];
            if (!TryStripFpInto(fp, 0, rawFp, 0)) return false;
            var rawRet = new byte[RawG1];
            if (ZiskCrypto.zkvm_bls12_map_fp_to_g1(rawFp, rawRet) != 0) return false;
            result = PadG1(rawRet);
            return true;
        }

        public bool TryMapFp2ToG2(byte[] fp2, out byte[] result)
        {
            result = null;
            if (fp2 == null || fp2.Length != EipFp2) return false;
            var rawFp2 = new byte[RawFp2];
            if (!TryStripFpInto(fp2, 0, rawFp2, 0)) return false;
            if (!TryStripFpInto(fp2, EipFp, rawFp2, RawFp)) return false;

            var rawRet = new byte[RawG2];
            if (ZiskCrypto.zkvm_bls12_map_fp2_to_g2(rawFp2, rawRet) != 0) return false;
            result = PadG2(rawRet);
            return true;
        }

        private static bool TryStripG1(byte[] eipG1, out byte[] raw)
        {
            raw = null;
            if (eipG1 == null || eipG1.Length != EipG1) return false;
            var stripped = new byte[RawG1];
            if (!TryStripFpInto(eipG1, 0, stripped, 0)) return false;
            if (!TryStripFpInto(eipG1, EipFp, stripped, RawFp)) return false;
            raw = stripped;
            return true;
        }

        private static byte[] PadG1(byte[] rawG1)
        {
            var padded = new byte[EipG1];
            PadFpInto(rawG1, 0, padded, 0);
            PadFpInto(rawG1, RawFp, padded, EipFp);
            return padded;
        }

        private static bool TryStripG2(byte[] eipG2, out byte[] raw)
        {
            raw = null;
            if (eipG2 == null || eipG2.Length != EipG2) return false;
            var stripped = new byte[RawG2];
            for (int limb = 0; limb < 4; limb++)
            {
                if (!TryStripFpInto(eipG2, limb * EipFp, stripped, limb * RawFp)) return false;
            }
            raw = stripped;
            return true;
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

        private static bool TryStripFpInto(byte[] src, int srcOff, byte[] dst, int dstOff)
        {
            if (src == null || src.Length - srcOff < EipFp) return false;
            for (int i = 0; i < 16; i++)
            {
                if (src[srcOff + i] != 0) return false;
            }
            Array.Copy(src, srcOff + 16, dst, dstOff, RawFp);
            return true;
        }

        private static void PadFpInto(byte[] rawSrc, int srcOff, byte[] paddedDst, int dstOff)
        {
            for (int i = 0; i < 16; i++) paddedDst[dstOff + i] = 0;
            Array.Copy(rawSrc, srcOff, paddedDst, dstOff + 16, RawFp);
        }
    }
}
