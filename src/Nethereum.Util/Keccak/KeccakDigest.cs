using System;
using System.Diagnostics;
using System.Text;

namespace Nethereum.Util.Keccak
{
    public class KeccakDigest
    {
        private static readonly int STATE_LENGTH = (1600 / 8);

        private ulong[] state = new ulong[STATE_LENGTH / 8];
        protected byte[] dataQueue = new byte[1536 / 8];
        protected int rate;
        protected int bitsInQueue;
        protected int fixedOutputLength;
        protected bool squeezing;
        protected int bitsAvailableForSqueezing;

        public KeccakDigest()
            : this(288)
        {
        }

        public KeccakDigest(int bitLength)
        {
            Init(bitLength);
        }

        public KeccakDigest(KeccakDigest source)
        {
            CopyIn(source);
        }

        private void CopyIn(KeccakDigest source)
        {
            Array.Copy(source.state, 0, this.state, 0, source.state.Length);
            Array.Copy(source.dataQueue, 0, this.dataQueue, 0, source.dataQueue.Length);
            this.rate = source.rate;
            this.bitsInQueue = source.bitsInQueue;
            this.fixedOutputLength = source.fixedOutputLength;
            this.squeezing = source.squeezing;
            this.bitsAvailableForSqueezing = source.bitsAvailableForSqueezing;
        }

        public virtual string AlgorithmName
        {
            get { return "Keccak-" + fixedOutputLength; }
        }

        public virtual int GetDigestSize()
        {
            return fixedOutputLength >> 3;
        }

        public virtual void Update(byte input)
        {
            Absorb(new byte[] {input}, 0, 1);
        }

        public virtual void BlockUpdate(byte[] input, int inOff, int len)
        {
            Absorb(input, inOff, len);
        }

        public virtual int DoFinal(byte[] output, int outOff)
        {
            Squeeze(output, outOff, fixedOutputLength >> 3);

            Reset();

            return GetDigestSize();
        }

        /*
         * TODO Possible API change to support partial-byte suffixes.
         */
        protected virtual int DoFinal(byte[] output, int outOff, byte partialByte, int partialBits)
        {
            if (partialBits > 0)
            {
                AbsorbBits(partialByte, partialBits);
            }

            Squeeze(output, outOff, fixedOutputLength >> 3);

            Reset();

            return GetDigestSize();
        }

        public virtual void Reset()
        {
            Init(fixedOutputLength);
        }

        /**
         * Return the size of block that the compression function is applied to in bytes.
         *
         * @return internal byte length of a block.
         */
        public virtual int GetByteLength()
        {
            return rate >> 3;
        }

        private void Init(int bitLength)
        {
            switch (bitLength)
            {
                case 128:
                case 224:
                case 256:
                case 288:
                case 384:
                case 512:
                    InitSponge(1600 - (bitLength << 1));
                    break;
                default:
                    throw new ArgumentException("must be one of 128, 224, 256, 288, 384, or 512.", "bitLength");
            }
        }

        private void InitSponge(int rate)
        {
            if (rate <= 0 || rate >= 1600 || (rate & 63) != 0)
                throw new InvalidOperationException("invalid rate value");

            this.rate = rate;
            Array.Clear(state, 0, state.Length);
            Arrays.Fill(this.dataQueue, (byte) 0);
            this.bitsInQueue = 0;
            this.squeezing = false;
            this.bitsAvailableForSqueezing = 0;
            this.fixedOutputLength = (1600 - rate) >> 1;
        }

        protected void Absorb(byte[] data, int off, int len)
        {
            if ((bitsInQueue & 7) != 0)
                throw new InvalidOperationException("attempt to absorb with odd length queue");
            if (squeezing)
                throw new InvalidOperationException("attempt to absorb while squeezing");

            int bytesInQueue = bitsInQueue >> 3;
            int rateBytes = rate >> 3;

            int count = 0;
            while (count < len)
            {
                if (bytesInQueue == 0 && count <= (len - rateBytes))
                {
                    do
                    {
                        KeccakAbsorb(data, off + count);
                        count += rateBytes;
                    } while (count <= (len - rateBytes));
                }
                else
                {
                    int partialBlock = System.Math.Min(rateBytes - bytesInQueue, len - count);
                    Array.Copy(data, off + count, dataQueue, bytesInQueue, partialBlock);

                    bytesInQueue += partialBlock;
                    count += partialBlock;

                    if (bytesInQueue == rateBytes)
                    {
                        KeccakAbsorb(dataQueue, 0);
                        bytesInQueue = 0;
                    }
                }
            }

            bitsInQueue = bytesInQueue << 3;
        }

        protected void AbsorbBits(int data, int bits)
        {
            if (bits < 1 || bits > 7)
                throw new ArgumentException("must be in the range 1 to 7", "bits");
            if ((bitsInQueue & 7) != 0)
                throw new InvalidOperationException("attempt to absorb with odd length queue");
            if (squeezing)
                throw new InvalidOperationException("attempt to absorb while squeezing");

            int mask = (1 << bits) - 1;
            dataQueue[bitsInQueue >> 3] = (byte) (data & mask);

            // NOTE: After this, bitsInQueue is no longer a multiple of 8, so no more absorbs will work
            bitsInQueue += bits;
        }

        private void PadAndSwitchToSqueezingPhase()
        {
            Debug.Assert(bitsInQueue < rate);

            dataQueue[bitsInQueue >> 3] |= (byte) (1U << (bitsInQueue & 7));

            if (++bitsInQueue == rate)
            {
                KeccakAbsorb(dataQueue, 0);
                bitsInQueue = 0;
            }

            {
                int full = bitsInQueue >> 6, partial = bitsInQueue & 63;
                int off = 0;
                for (int i = 0; i < full; ++i)
                {
                    state[i] ^= Pack.LE_To_UInt64(dataQueue, off);
                    off += 8;
                }

                if (partial > 0)
                {
                    ulong mask = (1UL << partial) - 1UL;
                    state[full] ^= Pack.LE_To_UInt64(dataQueue, off) & mask;
                }

                state[(rate - 1) >> 6] ^= (1UL << 63);
            }

            KeccakPermutation();
            KeccakExtract();
            bitsAvailableForSqueezing = rate;

            bitsInQueue = 0;
            squeezing = true;
        }

        protected void Squeeze(byte[] output, int off, int len)
        {
            if (!squeezing)
            {
                PadAndSwitchToSqueezingPhase();
            }

            long outputLength = (long) len << 3;
            long i = 0;
            while (i < outputLength)
            {
                if (bitsAvailableForSqueezing == 0)
                {
                    KeccakPermutation();
                    KeccakExtract();
                    bitsAvailableForSqueezing = rate;
                }

                int partialBlock = (int) System.Math.Min((long) bitsAvailableForSqueezing, outputLength - i);
                Array.Copy(dataQueue, (rate - bitsAvailableForSqueezing) >> 3, output, off + (int) (i >> 3),
                    partialBlock >> 3);
                bitsAvailableForSqueezing -= partialBlock;
                i += partialBlock;
            }
        }

        private void KeccakAbsorb(byte[] data, int off)
        {
            KeccakF1600.XorLanes(state, data, off, rate >> 6);
            KeccakPermutation();
        }

        private void KeccakExtract()
        {
            Pack.UInt64_To_LE(state, 0, rate >> 6, dataQueue, 0);
        }

        private void KeccakPermutation()
        {
            KeccakF1600.Permute(state);
        }
    }

    internal static class KeccakF1600
    {
        internal const int StateLanes = 25;
        internal const int Keccak256RateBytes = 136;
        internal const int Keccak256RateLanes = 17;
        internal const int Keccak256OutputBytes = 32;

        private static readonly ulong[] RoundConstants =
        {
            0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808AUL, 0x8000000080008000UL,
            0x000000000000808BUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
            0x000000000000008AUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000AUL,
            0x000000008000808BUL, 0x800000000000008BUL, 0x8000000000008089UL, 0x8000000000008003UL,
            0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800AUL, 0x800000008000000AUL,
            0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL
        };

        [ThreadStatic] private static ulong[] _threadState;

        internal static void Keccak256(byte[] input, int offset, int length, byte[] output, int outputOffset)
        {
            var state = _threadState ?? (_threadState = new ulong[StateLanes]);
            Array.Clear(state, 0, StateLanes);

            while (length >= Keccak256RateBytes)
            {
                XorLanes(state, input, offset, Keccak256RateLanes);
                Permute(state);
                offset += Keccak256RateBytes;
                length -= Keccak256RateBytes;
            }

            var fullLanes = length >> 3;
            XorLanes(state, input, offset, fullLanes);
            offset += fullLanes << 3;

            var tailBytes = length & 7;
            var tail = 0UL;
            for (var i = 0; i < tailBytes; i++)
            {
                tail |= (ulong)input[offset + i] << (i << 3);
            }

            state[fullLanes] ^= tail | (1UL << (tailBytes << 3));
            state[Keccak256RateLanes - 1] ^= 0x8000000000000000UL;
            Permute(state);

            StoreLane(state[0], output, outputOffset);
            StoreLane(state[1], output, outputOffset + 8);
            StoreLane(state[2], output, outputOffset + 16);
            StoreLane(state[3], output, outputOffset + 24);
        }

        internal static void XorLanes(ulong[] state, byte[] data, int offset, int laneCount)
        {
            for (var i = 0; i < laneCount; i++)
            {
                state[i] ^= LoadLane(data, offset);
                offset += 8;
            }
        }

        internal static ulong LoadLane(byte[] data, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToUInt64(data, offset);
            }

            return data[offset]
                   | (ulong)data[offset + 1] << 8
                   | (ulong)data[offset + 2] << 16
                   | (ulong)data[offset + 3] << 24
                   | (ulong)data[offset + 4] << 32
                   | (ulong)data[offset + 5] << 40
                   | (ulong)data[offset + 6] << 48
                   | (ulong)data[offset + 7] << 56;
        }

        internal static void StoreLane(ulong lane, byte[] data, int offset)
        {
            data[offset + 7] = (byte)(lane >> 56);
            data[offset] = (byte)lane;
            data[offset + 1] = (byte)(lane >> 8);
            data[offset + 2] = (byte)(lane >> 16);
            data[offset + 3] = (byte)(lane >> 24);
            data[offset + 4] = (byte)(lane >> 32);
            data[offset + 5] = (byte)(lane >> 40);
            data[offset + 6] = (byte)(lane >> 48);
        }

        internal static void Permute(ulong[] state)
        {
            var asu = state[24];
            var aba = state[0];
            var abe = state[1];
            var abi = state[2];
            var abo = state[3];
            var abu = state[4];
            var aga = state[5];
            var age = state[6];
            var agi = state[7];
            var ago = state[8];
            var agu = state[9];
            var aka = state[10];
            var ake = state[11];
            var aki = state[12];
            var ako = state[13];
            var aku = state[14];
            var ama = state[15];
            var ame = state[16];
            var ami = state[17];
            var amo = state[18];
            var amu = state[19];
            var asa = state[20];
            var ase = state[21];
            var asi = state[22];
            var aso = state[23];

            ulong eba, ebe, ebi, ebo, ebu, ega, ege, egi, ego, egu, eka, eke, eki, eko, eku;
            ulong ema, eme, emi, emo, emu, esa, ese, esi, eso, esu;
            ulong b0, b1, b2, b3, b4, d0, d1, d2, d3, d4, t;

            var c0 = aba ^ aga ^ aka ^ ama ^ asa;
            var c1 = abe ^ age ^ ake ^ ame ^ ase;
            var c2 = abi ^ agi ^ aki ^ ami ^ asi;
            var c3 = abo ^ ago ^ ako ^ amo ^ aso;
            var c4 = abu ^ agu ^ aku ^ amu ^ asu;

            var rc = RoundConstants;
            for (var round = 0; round < 24; round += 2)
            {
                d0 = c4 ^ ((c1 << 1) | (c1 >> 63));
                d1 = c0 ^ ((c2 << 1) | (c2 >> 63));
                d2 = c1 ^ ((c3 << 1) | (c3 >> 63));
                d3 = c2 ^ ((c4 << 1) | (c4 >> 63));
                d4 = c3 ^ ((c0 << 1) | (c0 >> 63));
                b0 = aba ^ d0;
                t = age ^ d1;
                b1 = (t << 44) | (t >> 20);
                t = aki ^ d2;
                b2 = (t << 43) | (t >> 21);
                t = amo ^ d3;
                b3 = (t << 21) | (t >> 43);
                t = asu ^ d4;
                b4 = (t << 14) | (t >> 50);
                eba = b0 ^ (~b1 & b2) ^ rc[round];
                ebe = b1 ^ (~b2 & b3);
                ebi = b2 ^ (~b3 & b4);
                ebo = b3 ^ (~b4 & b0);
                ebu = b4 ^ (~b0 & b1);
                t = abo ^ d3;
                b0 = (t << 28) | (t >> 36);
                t = agu ^ d4;
                b1 = (t << 20) | (t >> 44);
                t = aka ^ d0;
                b2 = (t << 3) | (t >> 61);
                t = ame ^ d1;
                b3 = (t << 45) | (t >> 19);
                t = asi ^ d2;
                b4 = (t << 61) | (t >> 3);
                ega = b0 ^ (~b1 & b2);
                ege = b1 ^ (~b2 & b3);
                egi = b2 ^ (~b3 & b4);
                ego = b3 ^ (~b4 & b0);
                egu = b4 ^ (~b0 & b1);
                t = abe ^ d1;
                b0 = (t << 1) | (t >> 63);
                t = agi ^ d2;
                b1 = (t << 6) | (t >> 58);
                t = ako ^ d3;
                b2 = (t << 25) | (t >> 39);
                t = amu ^ d4;
                b3 = (t << 8) | (t >> 56);
                t = asa ^ d0;
                b4 = (t << 18) | (t >> 46);
                eka = b0 ^ (~b1 & b2);
                eke = b1 ^ (~b2 & b3);
                eki = b2 ^ (~b3 & b4);
                eko = b3 ^ (~b4 & b0);
                eku = b4 ^ (~b0 & b1);
                t = abu ^ d4;
                b0 = (t << 27) | (t >> 37);
                t = aga ^ d0;
                b1 = (t << 36) | (t >> 28);
                t = ake ^ d1;
                b2 = (t << 10) | (t >> 54);
                t = ami ^ d2;
                b3 = (t << 15) | (t >> 49);
                t = aso ^ d3;
                b4 = (t << 56) | (t >> 8);
                ema = b0 ^ (~b1 & b2);
                eme = b1 ^ (~b2 & b3);
                emi = b2 ^ (~b3 & b4);
                emo = b3 ^ (~b4 & b0);
                emu = b4 ^ (~b0 & b1);
                t = abi ^ d2;
                b0 = (t << 62) | (t >> 2);
                t = ago ^ d3;
                b1 = (t << 55) | (t >> 9);
                t = aku ^ d4;
                b2 = (t << 39) | (t >> 25);
                t = ama ^ d0;
                b3 = (t << 41) | (t >> 23);
                t = ase ^ d1;
                b4 = (t << 2) | (t >> 62);
                esa = b0 ^ (~b1 & b2);
                ese = b1 ^ (~b2 & b3);
                esi = b2 ^ (~b3 & b4);
                eso = b3 ^ (~b4 & b0);
                esu = b4 ^ (~b0 & b1);
                c0 = eba ^ ega ^ eka ^ ema ^ esa;
                c1 = ebe ^ ege ^ eke ^ eme ^ ese;
                c2 = ebi ^ egi ^ eki ^ emi ^ esi;
                c3 = ebo ^ ego ^ eko ^ emo ^ eso;
                c4 = ebu ^ egu ^ eku ^ emu ^ esu;
                d0 = c4 ^ ((c1 << 1) | (c1 >> 63));
                d1 = c0 ^ ((c2 << 1) | (c2 >> 63));
                d2 = c1 ^ ((c3 << 1) | (c3 >> 63));
                d3 = c2 ^ ((c4 << 1) | (c4 >> 63));
                d4 = c3 ^ ((c0 << 1) | (c0 >> 63));
                b0 = eba ^ d0;
                t = ege ^ d1;
                b1 = (t << 44) | (t >> 20);
                t = eki ^ d2;
                b2 = (t << 43) | (t >> 21);
                t = emo ^ d3;
                b3 = (t << 21) | (t >> 43);
                t = esu ^ d4;
                b4 = (t << 14) | (t >> 50);
                aba = b0 ^ (~b1 & b2) ^ rc[round + 1];
                abe = b1 ^ (~b2 & b3);
                abi = b2 ^ (~b3 & b4);
                abo = b3 ^ (~b4 & b0);
                abu = b4 ^ (~b0 & b1);
                t = ebo ^ d3;
                b0 = (t << 28) | (t >> 36);
                t = egu ^ d4;
                b1 = (t << 20) | (t >> 44);
                t = eka ^ d0;
                b2 = (t << 3) | (t >> 61);
                t = eme ^ d1;
                b3 = (t << 45) | (t >> 19);
                t = esi ^ d2;
                b4 = (t << 61) | (t >> 3);
                aga = b0 ^ (~b1 & b2);
                age = b1 ^ (~b2 & b3);
                agi = b2 ^ (~b3 & b4);
                ago = b3 ^ (~b4 & b0);
                agu = b4 ^ (~b0 & b1);
                t = ebe ^ d1;
                b0 = (t << 1) | (t >> 63);
                t = egi ^ d2;
                b1 = (t << 6) | (t >> 58);
                t = eko ^ d3;
                b2 = (t << 25) | (t >> 39);
                t = emu ^ d4;
                b3 = (t << 8) | (t >> 56);
                t = esa ^ d0;
                b4 = (t << 18) | (t >> 46);
                aka = b0 ^ (~b1 & b2);
                ake = b1 ^ (~b2 & b3);
                aki = b2 ^ (~b3 & b4);
                ako = b3 ^ (~b4 & b0);
                aku = b4 ^ (~b0 & b1);
                t = ebu ^ d4;
                b0 = (t << 27) | (t >> 37);
                t = ega ^ d0;
                b1 = (t << 36) | (t >> 28);
                t = eke ^ d1;
                b2 = (t << 10) | (t >> 54);
                t = emi ^ d2;
                b3 = (t << 15) | (t >> 49);
                t = eso ^ d3;
                b4 = (t << 56) | (t >> 8);
                ama = b0 ^ (~b1 & b2);
                ame = b1 ^ (~b2 & b3);
                ami = b2 ^ (~b3 & b4);
                amo = b3 ^ (~b4 & b0);
                amu = b4 ^ (~b0 & b1);
                t = ebi ^ d2;
                b0 = (t << 62) | (t >> 2);
                t = ego ^ d3;
                b1 = (t << 55) | (t >> 9);
                t = eku ^ d4;
                b2 = (t << 39) | (t >> 25);
                t = ema ^ d0;
                b3 = (t << 41) | (t >> 23);
                t = ese ^ d1;
                b4 = (t << 2) | (t >> 62);
                asa = b0 ^ (~b1 & b2);
                ase = b1 ^ (~b2 & b3);
                asi = b2 ^ (~b3 & b4);
                aso = b3 ^ (~b4 & b0);
                asu = b4 ^ (~b0 & b1);
                c0 = aba ^ aga ^ aka ^ ama ^ asa;
                c1 = abe ^ age ^ ake ^ ame ^ ase;
                c2 = abi ^ agi ^ aki ^ ami ^ asi;
                c3 = abo ^ ago ^ ako ^ amo ^ aso;
                c4 = abu ^ agu ^ aku ^ amu ^ asu;
            }

            state[24] = asu;
            state[0] = aba;
            state[1] = abe;
            state[2] = abi;
            state[3] = abo;
            state[4] = abu;
            state[5] = aga;
            state[6] = age;
            state[7] = agi;
            state[8] = ago;
            state[9] = agu;
            state[10] = aka;
            state[11] = ake;
            state[12] = aki;
            state[13] = ako;
            state[14] = aku;
            state[15] = ama;
            state[16] = ame;
            state[17] = ami;
            state[18] = amo;
            state[19] = amu;
            state[20] = asa;
            state[21] = ase;
            state[22] = asi;
            state[23] = aso;
        }
    }

    internal sealed class Pack
    {
        private Pack()
        {
        }

        internal static void UInt16_To_BE(ushort n, byte[] bs)
        {
            bs[0] = (byte) (n >> 8);
            bs[1] = (byte) (n);
        }

        internal static void UInt16_To_BE(ushort n, byte[] bs, int off)
        {
            bs[off] = (byte) (n >> 8);
            bs[off + 1] = (byte) (n);
        }

        internal static ushort BE_To_UInt16(byte[] bs)
        {
            uint n = (uint) bs[0] << 8
                     | (uint) bs[1];
            return (ushort) n;
        }

        internal static ushort BE_To_UInt16(byte[] bs, int off)
        {
            uint n = (uint) bs[off] << 8
                     | (uint) bs[off + 1];
            return (ushort) n;
        }

        internal static byte[] UInt32_To_BE(uint n)
        {
            byte[] bs = new byte[4];
            UInt32_To_BE(n, bs, 0);
            return bs;
        }

        internal static void UInt32_To_BE(uint n, byte[] bs)
        {
            bs[0] = (byte) (n >> 24);
            bs[1] = (byte) (n >> 16);
            bs[2] = (byte) (n >> 8);
            bs[3] = (byte) (n);
        }

        internal static void UInt32_To_BE(uint n, byte[] bs, int off)
        {
            bs[off] = (byte) (n >> 24);
            bs[off + 1] = (byte) (n >> 16);
            bs[off + 2] = (byte) (n >> 8);
            bs[off + 3] = (byte) (n);
        }

        internal static byte[] UInt32_To_BE(uint[] ns)
        {
            byte[] bs = new byte[4 * ns.Length];
            UInt32_To_BE(ns, bs, 0);
            return bs;
        }

        internal static void UInt32_To_BE(uint[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt32_To_BE(ns[i], bs, off);
                off += 4;
            }
        }

        internal static uint BE_To_UInt32(byte[] bs)
        {
            return (uint) bs[0] << 24
                   | (uint) bs[1] << 16
                   | (uint) bs[2] << 8
                   | (uint) bs[3];
        }

        internal static uint BE_To_UInt32(byte[] bs, int off)
        {
            return (uint) bs[off] << 24
                   | (uint) bs[off + 1] << 16
                   | (uint) bs[off + 2] << 8
                   | (uint) bs[off + 3];
        }

        internal static void BE_To_UInt32(byte[] bs, int off, uint[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt32(bs, off);
                off += 4;
            }
        }

        internal static byte[] UInt64_To_BE(ulong n)
        {
            byte[] bs = new byte[8];
            UInt64_To_BE(n, bs, 0);
            return bs;
        }

        internal static void UInt64_To_BE(ulong n, byte[] bs)
        {
            UInt32_To_BE((uint) (n >> 32), bs);
            UInt32_To_BE((uint) (n), bs, 4);
        }

        internal static void UInt64_To_BE(ulong n, byte[] bs, int off)
        {
            UInt32_To_BE((uint) (n >> 32), bs, off);
            UInt32_To_BE((uint) (n), bs, off + 4);
        }

        internal static byte[] UInt64_To_BE(ulong[] ns)
        {
            byte[] bs = new byte[8 * ns.Length];
            UInt64_To_BE(ns, bs, 0);
            return bs;
        }

        internal static void UInt64_To_BE(ulong[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt64_To_BE(ns[i], bs, off);
                off += 8;
            }
        }

        internal static ulong BE_To_UInt64(byte[] bs)
        {
            uint hi = BE_To_UInt32(bs);
            uint lo = BE_To_UInt32(bs, 4);
            return ((ulong) hi << 32) | (ulong) lo;
        }

        internal static ulong BE_To_UInt64(byte[] bs, int off)
        {
            uint hi = BE_To_UInt32(bs, off);
            uint lo = BE_To_UInt32(bs, off + 4);
            return ((ulong) hi << 32) | (ulong) lo;
        }

        internal static void BE_To_UInt64(byte[] bs, int off, ulong[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt64(bs, off);
                off += 8;
            }
        }

        internal static void UInt16_To_LE(ushort n, byte[] bs)
        {
            bs[0] = (byte) (n);
            bs[1] = (byte) (n >> 8);
        }

        internal static void UInt16_To_LE(ushort n, byte[] bs, int off)
        {
            bs[off] = (byte) (n);
            bs[off + 1] = (byte) (n >> 8);
        }

        internal static ushort LE_To_UInt16(byte[] bs)
        {
            uint n = (uint) bs[0]
                     | (uint) bs[1] << 8;
            return (ushort) n;
        }

        internal static ushort LE_To_UInt16(byte[] bs, int off)
        {
            uint n = (uint) bs[off]
                     | (uint) bs[off + 1] << 8;
            return (ushort) n;
        }

        internal static byte[] UInt32_To_LE(uint n)
        {
            byte[] bs = new byte[4];
            UInt32_To_LE(n, bs, 0);
            return bs;
        }

        internal static void UInt32_To_LE(uint n, byte[] bs)
        {
            bs[0] = (byte) (n);
            bs[1] = (byte) (n >> 8);
            bs[2] = (byte) (n >> 16);
            bs[3] = (byte) (n >> 24);
        }

        internal static void UInt32_To_LE(uint n, byte[] bs, int off)
        {
            bs[off] = (byte) (n);
            bs[off + 1] = (byte) (n >> 8);
            bs[off + 2] = (byte) (n >> 16);
            bs[off + 3] = (byte) (n >> 24);
        }

        internal static byte[] UInt32_To_LE(uint[] ns)
        {
            byte[] bs = new byte[4 * ns.Length];
            UInt32_To_LE(ns, bs, 0);
            return bs;
        }

        internal static void UInt32_To_LE(uint[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt32_To_LE(ns[i], bs, off);
                off += 4;
            }
        }

        internal static uint LE_To_UInt32(byte[] bs)
        {
            return (uint) bs[0]
                   | (uint) bs[1] << 8
                   | (uint) bs[2] << 16
                   | (uint) bs[3] << 24;
        }

        internal static uint LE_To_UInt32(byte[] bs, int off)
        {
            return (uint) bs[off]
                   | (uint) bs[off + 1] << 8
                   | (uint) bs[off + 2] << 16
                   | (uint) bs[off + 3] << 24;
        }

        internal static void LE_To_UInt32(byte[] bs, int off, uint[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt32(bs, off);
                off += 4;
            }
        }

        internal static void LE_To_UInt32(byte[] bs, int bOff, uint[] ns, int nOff, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                ns[nOff + i] = LE_To_UInt32(bs, bOff);
                bOff += 4;
            }
        }

        internal static uint[] LE_To_UInt32(byte[] bs, int off, int count)
        {
            uint[] ns = new uint[count];
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt32(bs, off);
                off += 4;
            }

            return ns;
        }

        internal static byte[] UInt64_To_LE(ulong n)
        {
            byte[] bs = new byte[8];
            UInt64_To_LE(n, bs, 0);
            return bs;
        }

        internal static void UInt64_To_LE(ulong n, byte[] bs)
        {
            UInt32_To_LE((uint) (n), bs);
            UInt32_To_LE((uint) (n >> 32), bs, 4);
        }

        internal static void UInt64_To_LE(ulong n, byte[] bs, int off)
        {
            UInt32_To_LE((uint) (n), bs, off);
            UInt32_To_LE((uint) (n >> 32), bs, off + 4);
        }

        internal static byte[] UInt64_To_LE(ulong[] ns)
        {
            byte[] bs = new byte[8 * ns.Length];
            UInt64_To_LE(ns, bs, 0);
            return bs;
        }

        internal static void UInt64_To_LE(ulong[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt64_To_LE(ns[i], bs, off);
                off += 8;
            }
        }

        internal static void UInt64_To_LE(ulong[] ns, int nsOff, int nsLen, byte[] bs, int bsOff)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                UInt64_To_LE(ns[nsOff + i], bs, bsOff);
                bsOff += 8;
            }
        }

        internal static ulong LE_To_UInt64(byte[] bs)
        {
            uint lo = LE_To_UInt32(bs);
            uint hi = LE_To_UInt32(bs, 4);
            return ((ulong) hi << 32) | (ulong) lo;
        }

        internal static ulong LE_To_UInt64(byte[] bs, int off)
        {
            uint lo = LE_To_UInt32(bs, off);
            uint hi = LE_To_UInt32(bs, off + 4);
            return ((ulong) hi << 32) | (ulong) lo;
        }

        internal static void LE_To_UInt64(byte[] bs, int off, ulong[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt64(bs, off);
                off += 8;
            }
        }

        internal static void LE_To_UInt64(byte[] bs, int bsOff, ulong[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = LE_To_UInt64(bs, bsOff);
                bsOff += 8;
            }
        }
    }

    /// <summary> General array utilities.</summary>
    internal abstract class Arrays
    {
        public static bool AreEqual(
            bool[] a,
            bool[] b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            return HaveSameContents(a, b);
        }

        public static bool AreEqual(
            char[] a,
            char[] b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            return HaveSameContents(a, b);
        }

        /// <summary>
        /// Are two arrays equal.
        /// </summary>
        /// <param name="a">Left side.</param>
        /// <param name="b">Right side.</param>
        /// <returns>True if equal.</returns>
        public static bool AreEqual(
            byte[] a,
            byte[] b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            return HaveSameContents(a, b);
        }

        [Obsolete("Use 'AreEqual' method instead")]
        public static bool AreSame(
            byte[] a,
            byte[] b)
        {
            return AreEqual(a, b);
        }

        /// <summary>
        /// A constant time equals comparison - does not terminate early if
        /// test will fail.
        /// </summary>
        /// <param name="a">first array</param>
        /// <param name="b">second array</param>
        /// <returns>true if arrays equal, false otherwise.</returns>
        public static bool ConstantTimeAreEqual(
            byte[] a,
            byte[] b)
        {
            int i = a.Length;
            if (i != b.Length)
                return false;
            int cmp = 0;
            while (i != 0)
            {
                --i;
                cmp |= (a[i] ^ b[i]);
            }

            return cmp == 0;
        }

        public static bool AreEqual(
            int[] a,
            int[] b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            return HaveSameContents(a, b);
        }

        public static bool AreEqual(uint[] a, uint[] b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            return HaveSameContents(a, b);
        }

        private static bool HaveSameContents(
            bool[] a,
            bool[] b)
        {
            int i = a.Length;
            if (i != b.Length)
                return false;
            while (i != 0)
            {
                --i;
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool HaveSameContents(
            char[] a,
            char[] b)
        {
            int i = a.Length;
            if (i != b.Length)
                return false;
            while (i != 0)
            {
                --i;
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool HaveSameContents(
            byte[] a,
            byte[] b)
        {
            int i = a.Length;
            if (i != b.Length)
                return false;
            while (i != 0)
            {
                --i;
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool HaveSameContents(
            int[] a,
            int[] b)
        {
            int i = a.Length;
            if (i != b.Length)
                return false;
            while (i != 0)
            {
                --i;
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool HaveSameContents(uint[] a, uint[] b)
        {
            int i = a.Length;
            if (i != b.Length)
                return false;
            while (i != 0)
            {
                --i;
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        public static string ToString(
            object[] a)
        {
            StringBuilder sb = new StringBuilder('[');
            if (a.Length > 0)
            {
                sb.Append(a[0]);
                for (int index = 1; index < a.Length; ++index)
                {
                    sb.Append(", ").Append(a[index]);
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        public static int GetHashCode(byte[] data)
        {
            if (data == null)
            {
                return 0;
            }

            int i = data.Length;
            int hc = i + 1;

            while (--i >= 0)
            {
                hc *= 257;
                hc ^= data[i];
            }

            return hc;
        }

        public static int GetHashCode(byte[] data, int off, int len)
        {
            if (data == null)
            {
                return 0;
            }

            int i = len;
            int hc = i + 1;

            while (--i >= 0)
            {
                hc *= 257;
                hc ^= data[off + i];
            }

            return hc;
        }

        public static int GetHashCode(int[] data)
        {
            if (data == null)
                return 0;

            int i = data.Length;
            int hc = i + 1;

            while (--i >= 0)
            {
                hc *= 257;
                hc ^= data[i];
            }

            return hc;
        }

        public static int GetHashCode(int[] data, int off, int len)
        {
            if (data == null)
                return 0;

            int i = len;
            int hc = i + 1;

            while (--i >= 0)
            {
                hc *= 257;
                hc ^= data[off + i];
            }

            return hc;
        }

        [CLSCompliantAttribute(false)]
        public static int GetHashCode(uint[] data)
        {
            if (data == null)
                return 0;

            int i = data.Length;
            int hc = i + 1;

            while (--i >= 0)
            {
                hc *= 257;
                hc ^= (int) data[i];
            }

            return hc;
        }

        [CLSCompliantAttribute(false)]
        public static int GetHashCode(uint[] data, int off, int len)
        {
            if (data == null)
                return 0;

            int i = len;
            int hc = i + 1;

            while (--i >= 0)
            {
                hc *= 257;
                hc ^= (int) data[off + i];
            }

            return hc;
        }

        [CLSCompliantAttribute(false)]
        public static int GetHashCode(ulong[] data)
        {
            if (data == null)
                return 0;

            int i = data.Length;
            int hc = i + 1;

            while (--i >= 0)
            {
                ulong di = data[i];
                hc *= 257;
                hc ^= (int) di;
                hc *= 257;
                hc ^= (int) (di >> 32);
            }

            return hc;
        }

        [CLSCompliantAttribute(false)]
        public static int GetHashCode(ulong[] data, int off, int len)
        {
            if (data == null)
                return 0;

            int i = len;
            int hc = i + 1;

            while (--i >= 0)
            {
                ulong di = data[off + i];
                hc *= 257;
                hc ^= (int) di;
                hc *= 257;
                hc ^= (int) (di >> 32);
            }

            return hc;
        }

        public static byte[] Clone(
            byte[] data)
        {
            return data == null ? null : (byte[]) data.Clone();
        }

        public static byte[] Clone(
            byte[] data,
            byte[] existing)
        {
            if (data == null)
            {
                return null;
            }

            if ((existing == null) || (existing.Length != data.Length))
            {
                return Clone(data);
            }

            Array.Copy(data, 0, existing, 0, existing.Length);
            return existing;
        }

        public static int[] Clone(
            int[] data)
        {
            return data == null ? null : (int[]) data.Clone();
        }

        internal static uint[] Clone(uint[] data)
        {
            return data == null ? null : (uint[]) data.Clone();
        }

        public static long[] Clone(long[] data)
        {
            return data == null ? null : (long[]) data.Clone();
        }

        [CLSCompliantAttribute(false)]
        public static ulong[] Clone(
            ulong[] data)
        {
            return data == null ? null : (ulong[]) data.Clone();
        }

        [CLSCompliantAttribute(false)]
        public static ulong[] Clone(
            ulong[] data,
            ulong[] existing)
        {
            if (data == null)
            {
                return null;
            }

            if ((existing == null) || (existing.Length != data.Length))
            {
                return Clone(data);
            }

            Array.Copy(data, 0, existing, 0, existing.Length);
            return existing;
        }

        public static bool Contains(byte[] a, byte n)
        {
            for (int i = 0; i < a.Length; ++i)
            {
                if (a[i] == n)
                    return true;
            }

            return false;
        }

        public static bool Contains(short[] a, short n)
        {
            for (int i = 0; i < a.Length; ++i)
            {
                if (a[i] == n)
                    return true;
            }

            return false;
        }

        public static bool Contains(int[] a, int n)
        {
            for (int i = 0; i < a.Length; ++i)
            {
                if (a[i] == n)
                    return true;
            }

            return false;
        }

        public static void Fill(
            byte[] buf,
            byte b)
        {
            int i = buf.Length;
            while (i > 0)
            {
                buf[--i] = b;
            }
        }

        public static byte[] CopyOf(byte[] data, int newLength)
        {
            byte[] tmp = new byte[newLength];
            Array.Copy(data, 0, tmp, 0, System.Math.Min(newLength, data.Length));
            return tmp;
        }

        public static char[] CopyOf(char[] data, int newLength)
        {
            char[] tmp = new char[newLength];
            Array.Copy(data, 0, tmp, 0, System.Math.Min(newLength, data.Length));
            return tmp;
        }

        public static int[] CopyOf(int[] data, int newLength)
        {
            int[] tmp = new int[newLength];
            Array.Copy(data, 0, tmp, 0, System.Math.Min(newLength, data.Length));
            return tmp;
        }

        public static long[] CopyOf(long[] data, int newLength)
        {
            long[] tmp = new long[newLength];
            Array.Copy(data, 0, tmp, 0, System.Math.Min(newLength, data.Length));
            return tmp;
        }


        /**
         * Make a copy of a range of bytes from the passed in data array. The range can
         * extend beyond the end of the input array, in which case the return array will
         * be padded with zeroes.
         *
         * @param data the array from which the data is to be copied.
         * @param from the start index at which the copying should take place.
         * @param to the final index of the range (exclusive).
         *
         * @return a new byte array containing the range given.
         */
        public static byte[] CopyOfRange(byte[] data, int from, int to)
        {
            int newLength = GetLength(from, to);
            byte[] tmp = new byte[newLength];
            Array.Copy(data, from, tmp, 0, System.Math.Min(newLength, data.Length - from));
            return tmp;
        }

        public static int[] CopyOfRange(int[] data, int from, int to)
        {
            int newLength = GetLength(from, to);
            int[] tmp = new int[newLength];
            Array.Copy(data, from, tmp, 0, System.Math.Min(newLength, data.Length - from));
            return tmp;
        }

        public static long[] CopyOfRange(long[] data, int from, int to)
        {
            int newLength = GetLength(from, to);
            long[] tmp = new long[newLength];
            Array.Copy(data, from, tmp, 0, System.Math.Min(newLength, data.Length - from));
            return tmp;
        }


        private static int GetLength(int from, int to)
        {
            int newLength = to - from;
            if (newLength < 0)
                throw new ArgumentException(from + " > " + to);
            return newLength;
        }

        public static byte[] Append(byte[] a, byte b)
        {
            if (a == null)
                return new byte[] {b};

            int length = a.Length;
            byte[] result = new byte[length + 1];
            Array.Copy(a, 0, result, 0, length);
            result[length] = b;
            return result;
        }

        public static short[] Append(short[] a, short b)
        {
            if (a == null)
                return new short[] {b};

            int length = a.Length;
            short[] result = new short[length + 1];
            Array.Copy(a, 0, result, 0, length);
            result[length] = b;
            return result;
        }

        public static int[] Append(int[] a, int b)
        {
            if (a == null)
                return new int[] {b};

            int length = a.Length;
            int[] result = new int[length + 1];
            Array.Copy(a, 0, result, 0, length);
            result[length] = b;
            return result;
        }

        public static byte[] Concatenate(byte[] a, byte[] b)
        {
            if (a == null)
                return Clone(b);
            if (b == null)
                return Clone(a);

            byte[] rv = new byte[a.Length + b.Length];
            Array.Copy(a, 0, rv, 0, a.Length);
            Array.Copy(b, 0, rv, a.Length, b.Length);
            return rv;
        }

        public static byte[] ConcatenateAll(params byte[][] vs)
        {
            byte[][] nonNull = new byte[vs.Length][];
            int count = 0;
            int totalLength = 0;

            for (int i = 0; i < vs.Length; ++i)
            {
                byte[] v = vs[i];
                if (v != null)
                {
                    nonNull[count++] = v;
                    totalLength += v.Length;
                }
            }

            byte[] result = new byte[totalLength];
            int pos = 0;

            for (int j = 0; j < count; ++j)
            {
                byte[] v = nonNull[j];
                Array.Copy(v, 0, result, pos, v.Length);
                pos += v.Length;
            }

            return result;
        }

        public static int[] Concatenate(int[] a, int[] b)
        {
            if (a == null)
                return Clone(b);
            if (b == null)
                return Clone(a);

            int[] rv = new int[a.Length + b.Length];
            Array.Copy(a, 0, rv, 0, a.Length);
            Array.Copy(b, 0, rv, a.Length, b.Length);
            return rv;
        }

        public static byte[] Prepend(byte[] a, byte b)
        {
            if (a == null)
                return new byte[] {b};

            int length = a.Length;
            byte[] result = new byte[length + 1];
            Array.Copy(a, 0, result, 1, length);
            result[0] = b;
            return result;
        }

        public static short[] Prepend(short[] a, short b)
        {
            if (a == null)
                return new short[] {b};

            int length = a.Length;
            short[] result = new short[length + 1];
            Array.Copy(a, 0, result, 1, length);
            result[0] = b;
            return result;
        }

        public static int[] Prepend(int[] a, int b)
        {
            if (a == null)
                return new int[] {b};

            int length = a.Length;
            int[] result = new int[length + 1];
            Array.Copy(a, 0, result, 1, length);
            result[0] = b;
            return result;
        }

        public static byte[] Reverse(byte[] a)
        {
            if (a == null)
                return null;

            int p1 = 0, p2 = a.Length;
            byte[] result = new byte[p2];

            while (--p2 >= 0)
            {
                result[p2] = a[p1++];
            }

            return result;
        }

        public static int[] Reverse(int[] a)
        {
            if (a == null)
                return null;

            int p1 = 0, p2 = a.Length;
            int[] result = new int[p2];

            while (--p2 >= 0)
            {
                result[p2] = a[p1++];
            }

            return result;
        }
    }
}