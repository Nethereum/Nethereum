using System;

namespace Nethereum.Zisk.Core
{
    /// <summary>
    /// Minimal RIPEMD-160 implementation for Zisk zkVM.
    /// Avoids BouncyCastle dependency (~3K symbols) for a single hash function.
    /// </summary>
    public static class Ripemd160
    {
        public static byte[] ComputeHash(byte[] input)
        {
            uint h0 = 0x67452301, h1 = 0xEFCDAB89, h2 = 0x98BADCFE, h3 = 0x10325476, h4 = 0xC3D2E1F0;

            int msgLen = input.Length;
            int padLen = 64 - ((msgLen + 9) % 64);
            if (padLen == 64) padLen = 0;
            byte[] padded = new byte[msgLen + 1 + padLen + 8];
            Array.Copy(input, padded, msgLen);
            padded[msgLen] = 0x80;
            ulong bitLen = (ulong)msgLen * 8;
            for (int i = 0; i < 8; i++)
                padded[padded.Length - 8 + i] = (byte)(bitLen >> (i * 8));

            for (int offset = 0; offset < padded.Length; offset += 64)
            {
                uint[] x = new uint[16];
                for (int i = 0; i < 16; i++)
                    x[i] = (uint)(padded[offset + i * 4] | (padded[offset + i * 4 + 1] << 8) |
                            (padded[offset + i * 4 + 2] << 16) | (padded[offset + i * 4 + 3] << 24));

                uint al = h0, bl = h1, cl = h2, dl = h3, el = h4;
                uint ar = h0, br = h1, cr = h2, dr = h3, er = h4;

                for (int j = 0; j < 80; j++)
                {
                    uint fl, rl, sl, kl;
                    uint fr, rr, sr, kr;

                    if (j < 16)      { fl = bl ^ cl ^ dl; kl = 0x00000000; }
                    else if (j < 32) { fl = (bl & cl) | (~bl & dl); kl = 0x5A827999; }
                    else if (j < 48) { fl = (bl | ~cl) ^ dl; kl = 0x6ED9EBA1; }
                    else if (j < 64) { fl = (bl & dl) | (cl & ~dl); kl = 0x8F1BBCDC; }
                    else             { fl = bl ^ (cl | ~dl); kl = 0xA953FD4E; }

                    if (j < 16)      { fr = br ^ (cr | ~dr); kr = 0x50A28BE6; }
                    else if (j < 32) { fr = (br & dr) | (cr & ~dr); kr = 0x5C4DD124; }
                    else if (j < 48) { fr = (br | ~cr) ^ dr; kr = 0x6D703EF3; }
                    else if (j < 64) { fr = (br & cr) | (~br & dr); kr = 0x7A6D76E9; }
                    else             { fr = br ^ cr ^ dr; kr = 0x00000000; }

                    rl = RL[j]; sl = SL[j];
                    rr = RR[j]; sr = SR[j];

                    uint tl = al + fl + x[rl] + kl;
                    tl = Rot(tl, (int)sl) + el;
                    al = el; el = dl; dl = Rot(cl, 10); cl = bl; bl = tl;

                    uint tr = ar + fr + x[rr] + kr;
                    tr = Rot(tr, (int)sr) + er;
                    ar = er; er = dr; dr = Rot(cr, 10); cr = br; br = tr;
                }

                uint t = h1 + cl + dr;
                h1 = h2 + dl + er;
                h2 = h3 + el + ar;
                h3 = h4 + al + br;
                h4 = h0 + bl + cr;
                h0 = t;
            }

            byte[] result = new byte[20];
            WriteLE(result, 0, h0);
            WriteLE(result, 4, h1);
            WriteLE(result, 8, h2);
            WriteLE(result, 12, h3);
            WriteLE(result, 16, h4);
            return result;
        }

        private static uint Rot(uint x, int n) => (x << n) | (x >> (32 - n));

        private static void WriteLE(byte[] buf, int off, uint val)
        {
            buf[off] = (byte)val; buf[off + 1] = (byte)(val >> 8);
            buf[off + 2] = (byte)(val >> 16); buf[off + 3] = (byte)(val >> 24);
        }

        private static readonly uint[] RL = {
            0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,
            7,4,13,1,10,6,15,3,12,0,9,5,2,14,11,8,
            3,10,14,4,9,15,8,1,2,7,0,6,13,11,5,12,
            1,9,11,10,0,8,12,4,13,3,7,15,14,5,6,2,
            4,0,5,9,7,12,2,10,14,1,3,8,11,6,15,13
        };

        private static readonly uint[] RR = {
            5,14,7,0,9,2,11,4,13,6,15,8,1,10,3,12,
            6,11,3,7,0,13,5,10,14,15,8,12,4,9,1,2,
            15,5,1,3,7,14,6,9,11,8,12,2,10,0,4,13,
            8,6,4,1,3,11,15,0,5,12,2,13,9,7,10,14,
            12,15,10,4,1,5,8,7,6,2,13,14,0,3,9,11
        };

        private static readonly uint[] SL = {
            11,14,15,12,5,8,7,9,11,13,14,15,6,7,9,8,
            7,6,8,13,11,9,7,15,7,12,15,9,11,7,13,12,
            11,13,6,7,14,9,13,15,14,8,13,6,5,12,7,5,
            11,12,14,15,14,15,9,8,9,14,5,6,8,6,5,12,
            9,15,5,11,6,8,13,12,5,12,13,14,11,8,5,6
        };

        private static readonly uint[] SR = {
            8,9,9,11,13,15,15,5,7,7,8,11,14,14,12,6,
            9,13,15,7,12,8,9,11,7,7,12,7,6,15,13,11,
            9,7,15,11,8,6,6,14,12,13,5,14,13,13,7,5,
            15,5,8,11,14,14,6,14,6,9,12,9,12,5,15,8,
            8,5,12,9,12,5,14,6,8,13,6,5,15,13,11,11
        };
    }
}
