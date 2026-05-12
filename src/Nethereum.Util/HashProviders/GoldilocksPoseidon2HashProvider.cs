using Nethereum.Util.Poseidon;

namespace Nethereum.Util.HashProviders
{
    public class GoldilocksPoseidon2HashProvider : IHashProvider
    {
        private const int WIDTH = 16;
        private const int RATE = 12;
        private const int DIGEST_LONGS = 4;

        public byte[] ComputeHash(byte[] data)
        {
            if (data == null || data.Length == 0)
                return DoPermute(new GoldilocksField[WIDTH]);

            var state = new GoldilocksField[WIDTH];
            int offset = 0;
            int rateIndex = 0;

            while (offset < data.Length)
            {
                int chunkSize = data.Length - offset;
                if (chunkSize > 8) chunkSize = 8;
                ulong val = 0;
                for (int i = 0; i < chunkSize; i++)
                    val |= (ulong)data[offset + i] << (i * 8);
                ulong xored = state[rateIndex].Value ^ val;
                if (xored >= GoldilocksField.P) xored -= GoldilocksField.P;
                state[rateIndex] = new GoldilocksField(xored);
                offset += chunkSize;
                rateIndex++;
                if (rateIndex == RATE)
                {
                    state = Poseidon2Core.Permute16(state);
                    rateIndex = 0;
                }
            }

            if (rateIndex > 0)
                state = Poseidon2Core.Permute16(state);

            return ExtractDigest(state);
        }

        private static byte[] DoPermute(GoldilocksField[] state)
        {
            var result = Poseidon2Core.Permute16(state);
            return ExtractDigest(result);
        }

        private static byte[] ExtractDigest(GoldilocksField[] state)
        {
            var result = new byte[32];
            for (int i = 0; i < DIGEST_LONGS; i++)
            {
                ulong v = state[i].Value;
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
