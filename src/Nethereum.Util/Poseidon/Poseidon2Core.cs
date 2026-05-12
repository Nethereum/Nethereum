using System.Runtime.CompilerServices;

namespace Nethereum.Util.Poseidon
{
    public static class Poseidon2Core
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MatmulM4(ref GoldilocksField a, ref GoldilocksField b, ref GoldilocksField c, ref GoldilocksField d)
        {
            var t0 = a + b;
            var t1 = c + d;
            var t2 = b + b + t1;
            var t3 = d + d + t0;
            var t1_2 = t1 + t1;
            var t0_2 = t0 + t0;
            var t4 = t1_2 + t1_2 + t3;
            var t5 = t0_2 + t0_2 + t2;
            a = t3 + t5;
            b = t5;
            c = t2 + t4;
            d = t4;
        }

        public static void MatmulExternal(GoldilocksField[] state, int width)
        {
            for (int i = 0; i < width / 4; i++)
            {
                int off = i * 4;
                MatmulM4(ref state[off], ref state[off + 1], ref state[off + 2], ref state[off + 3]);
            }

            if (width > 4)
            {
                var stored = new GoldilocksField[4];
                for (int i = 0; i < 4; i++)
                {
                    var sum = GoldilocksField.Zero;
                    for (int j = 0; j < width / 4; j++)
                        sum = sum + state[j * 4 + i];
                    stored[i] = sum;
                }

                for (int i = 0; i < width; i++)
                    state[i] = state[i] + stored[i % 4];
            }
        }

        public static GoldilocksField[] Permute(
            GoldilocksField[] input,
            int width,
            int halfRounds,
            int nPartialRounds,
            ulong[] rc,
            ulong[] diag)
        {
            var state = new GoldilocksField[width];
            for (int i = 0; i < width; i++)
                state[i] = input[i];

            MatmulExternal(state, width);

            int rcIdx = 0;

            for (int r = 0; r < halfRounds; r++)
            {
                for (int i = 0; i < width; i++)
                    state[i] = state[i] + new GoldilocksField(rc[rcIdx++]);
                for (int i = 0; i < width; i++)
                    state[i] = GoldilocksField.Pow7(state[i]);
                MatmulExternal(state, width);
            }

            for (int r = 0; r < nPartialRounds; r++)
            {
                state[0] = state[0] + new GoldilocksField(rc[rcIdx++]);
                state[0] = GoldilocksField.Pow7(state[0]);
                var sum = GoldilocksField.Zero;
                for (int i = 0; i < width; i++)
                    sum = sum + state[i];
                for (int i = 0; i < width; i++)
                    state[i] = state[i] * new GoldilocksField(diag[i]) + sum;
            }

            for (int r = 0; r < halfRounds; r++)
            {
                for (int i = 0; i < width; i++)
                    state[i] = state[i] + new GoldilocksField(rc[rcIdx++]);
                for (int i = 0; i < width; i++)
                    state[i] = GoldilocksField.Pow7(state[i]);
                MatmulExternal(state, width);
            }

            return state;
        }

        public static GoldilocksField[] Permute16(GoldilocksField[] input)
        {
            return Permute(input, 16,
                Poseidon2GoldilocksConstants.P16_HALF_ROUNDS,
                Poseidon2GoldilocksConstants.P16_N_PARTIAL_ROUNDS,
                Poseidon2GoldilocksConstants.P16_RC,
                Poseidon2GoldilocksConstants.P16_DIAG);
        }
    }
}
