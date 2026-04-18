using System.Runtime.InteropServices;
using Nethereum.Util.HashProviders;

namespace Nethereum.Zisk.Core
{
    public class ZiskPoseidonHashProvider : IHashProvider
    {
        private const int STATE_WIDTH = 16;
        private const int RATE = 8;
        private const int DIGEST_LONGS = 4;

        [DllImport("__Internal")]
        private static extern unsafe void zkvm_poseidon2(ulong* state);

        public byte[] ComputeHash(byte[] data)
        {
            if (data == null || data.Length == 0)
                return HashEmpty();

            if (data.Length == 32)
                return HashSingle(data);

            if (data.Length == 64)
                return HashPair(data);

            return HashGeneric(data);
        }

        private static unsafe void Permute(ulong* state)
        {
            zkvm_poseidon2(state);
        }

        private static unsafe byte[] HashEmpty()
        {
            var state = stackalloc ulong[STATE_WIDTH];
            for (int i = 0; i < STATE_WIDTH; i++) state[i] = 0;
            Permute(state);
            return ExtractDigest(state);
        }

        private static unsafe byte[] HashSingle(byte[] data)
        {
            var state = stackalloc ulong[STATE_WIDTH];
            for (int i = 0; i < STATE_WIDTH; i++) state[i] = 0;
            AbsorbBytes(state, data, 0, 32);
            Permute(state);
            return ExtractDigest(state);
        }

        private static unsafe byte[] HashPair(byte[] data)
        {
            var state = stackalloc ulong[STATE_WIDTH];
            for (int i = 0; i < STATE_WIDTH; i++) state[i] = 0;
            AbsorbBytes(state, data, 0, 32);
            AbsorbBytes(state, data, 32, 32);
            Permute(state);
            return ExtractDigest(state);
        }

        private static unsafe byte[] HashGeneric(byte[] data)
        {
            var state = stackalloc ulong[STATE_WIDTH];
            for (int i = 0; i < STATE_WIDTH; i++) state[i] = 0;
            int offset = 0;
            int rateIndex = 0;

            while (offset < data.Length)
            {
                int chunkSize = data.Length - offset;
                if (chunkSize > 8) chunkSize = 8;

                ulong val = 0;
                for (int i = 0; i < chunkSize; i++)
                    val |= (ulong)data[offset + i] << (i * 8);

                state[rateIndex] ^= val;
                offset += chunkSize;
                rateIndex++;

                if (rateIndex == RATE)
                {
                    Permute(state);
                    rateIndex = 0;
                }
            }

            if (rateIndex > 0)
                Permute(state);

            return ExtractDigest(state);
        }

        private static unsafe void AbsorbBytes(ulong* state, byte[] data, int dataOffset, int length)
        {
            int stateIdx = 0;
            int pos = dataOffset;
            int end = dataOffset + length;

            while (pos < end && stateIdx < RATE)
            {
                int chunkSize = end - pos;
                if (chunkSize > 8) chunkSize = 8;

                ulong val = 0;
                for (int i = 0; i < chunkSize; i++)
                    val |= (ulong)data[pos + i] << (i * 8);

                state[stateIdx] ^= val;
                pos += chunkSize;
                stateIdx++;
            }
        }

        private static unsafe byte[] ExtractDigest(ulong* state)
        {
            var result = new byte[32];
            for (int i = 0; i < DIGEST_LONGS; i++)
            {
                ulong v = state[i];
                result[i * 8] = (byte)v;
                result[i * 8 + 1] = (byte)(v >> 8);
                result[i * 8 + 2] = (byte)(v >> 16);
                result[i * 8 + 3] = (byte)(v >> 24);
                result[i * 8 + 4] = (byte)(v >> 32);
                result[i * 8 + 5] = (byte)(v >> 40);
                result[i * 8 + 6] = (byte)(v >> 48);
                result[i * 8 + 7] = (byte)(v >> 56);
            }
            return result;
        }
    }
}
