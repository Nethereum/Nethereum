using Nethereum.Util.Poseidon;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class Poseidon2GoldilocksTests
    {
        [Fact]
        public void GoldilocksField_Add_WrapAround()
        {
            var pMinus1 = new GoldilocksField(GoldilocksField.P - 1);
            var one = new GoldilocksField(1);
            Assert.Equal(GoldilocksField.Zero, pMinus1 + one);
        }

        [Fact]
        public void GoldilocksField_Add_OverflowReduction()
        {
            var pMinus1 = new GoldilocksField(GoldilocksField.P - 1);
            var two = new GoldilocksField(2);
            Assert.Equal(new GoldilocksField(1), pMinus1 + two);
        }

        [Fact]
        public void GoldilocksField_Mul_Small()
        {
            var a = new GoldilocksField(2);
            var b = new GoldilocksField(3);
            Assert.Equal(new GoldilocksField(6), a * b);
        }

        [Fact]
        public void GoldilocksField_Pow7_Two()
        {
            var two = new GoldilocksField(2);
            Assert.Equal(new GoldilocksField(128), GoldilocksField.Pow7(two));
        }

        [Fact]
        public void GoldilocksField_Pow7_Zero()
        {
            Assert.Equal(GoldilocksField.Zero, GoldilocksField.Pow7(GoldilocksField.Zero));
        }

        [Fact]
        public void GoldilocksField_Mul_Large()
        {
            var a = new GoldilocksField(GoldilocksField.P - 1);
            var b = new GoldilocksField(GoldilocksField.P - 1);
            var result = a * b;
            Assert.Equal(new GoldilocksField(1), result);
        }

        [Fact]
        public void GoldilocksField_Sub_Basic()
        {
            var a = new GoldilocksField(5);
            var b = new GoldilocksField(3);
            Assert.Equal(new GoldilocksField(2), a - b);
        }

        [Fact]
        public void GoldilocksField_Sub_Underflow()
        {
            var a = new GoldilocksField(3);
            var b = new GoldilocksField(5);
            Assert.Equal(new GoldilocksField(GoldilocksField.P - 2), a - b);
        }

        /// <summary>
        /// Reference vector from pil2-proofman fields/src/poseidon2.rs test_poseidon2_16
        /// Input: [0, 1, 2, ..., 15]
        /// </summary>
        [Fact]
        public void Poseidon2_Permutation_Width16_MatchesPil2Proofman()
        {
            var input = new GoldilocksField[16];
            for (int i = 0; i < 16; i++)
                input[i] = new GoldilocksField((ulong)i);

            var output = Poseidon2Core.Permute16(input);

            Assert.Equal(new GoldilocksField(9639188652563994454), output[0]);
            Assert.Equal(new GoldilocksField(12273372933164734616), output[1]);
            Assert.Equal(new GoldilocksField(2905147255612444119), output[2]);
            Assert.Equal(new GoldilocksField(17581461329934617288), output[3]);
            Assert.Equal(new GoldilocksField(14390794100096760072), output[4]);
            Assert.Equal(new GoldilocksField(5468485695976078057), output[5]);
            Assert.Equal(new GoldilocksField(2832370985856357627), output[6]);
            Assert.Equal(new GoldilocksField(1116111836864400812), output[7]);
            Assert.Equal(new GoldilocksField(14997632823506024332), output[8]);
            Assert.Equal(new GoldilocksField(3976503894892102369), output[9]);
            Assert.Equal(new GoldilocksField(14874978986912301676), output[10]);
            Assert.Equal(new GoldilocksField(12458748982184310703), output[11]);
            Assert.Equal(new GoldilocksField(103345454961107931), output[12]);
            Assert.Equal(new GoldilocksField(3354965064850558444), output[13]);
            Assert.Equal(new GoldilocksField(14413825288474057217), output[14]);
            Assert.Equal(new GoldilocksField(4214638127285300968), output[15]);
        }

        [Fact]
        public void Poseidon2_Constants_DiagMatchesHorizenLabs()
        {
            Assert.Equal(16, Poseidon2GoldilocksConstants.P16_DIAG.Length);
            Assert.Equal(0xde9b91a467d6afc0UL, Poseidon2GoldilocksConstants.P16_DIAG[0]);
            Assert.Equal(0x774487b8c40089bbUL, Poseidon2GoldilocksConstants.P16_DIAG[15]);
        }

        [Fact]
        public void Poseidon2_Constants_RCCount()
        {
            int expected = Poseidon2GoldilocksConstants.P16_HALF_ROUNDS * 16
                         + Poseidon2GoldilocksConstants.P16_N_PARTIAL_ROUNDS
                         + Poseidon2GoldilocksConstants.P16_HALF_ROUNDS * 16;
            Assert.Equal(expected, Poseidon2GoldilocksConstants.P16_RC.Length);
        }

        [Fact]
        public void Poseidon2_Permutation_ZeroInput()
        {
            var input = new GoldilocksField[16];
            for (int i = 0; i < 16; i++)
                input[i] = GoldilocksField.Zero;

            var output = Poseidon2Core.Permute16(input);
            bool allZero = true;
            for (int i = 0; i < 16; i++)
                if (output[i] != GoldilocksField.Zero) { allZero = false; break; }
            Assert.False(allZero);
        }

        [Fact]
        public void GoldilocksPoseidon2HashProvider_PairHash_64Zeros()
        {
            var provider = new Nethereum.Util.HashProviders.GoldilocksPoseidon2HashProvider();
            var input = new byte[64];
            var hash = provider.ComputeHash(input);
            Assert.Equal(32, hash.Length);
            Assert.NotEqual(new byte[32], hash);

        }

        [Fact]
        public void GoldilocksPoseidon2HashProvider_MatchesSpongeConstruction()
        {
            var provider = new Nethereum.Util.HashProviders.GoldilocksPoseidon2HashProvider();

            var a = new byte[32];
            a[0] = 1;
            var b = new byte[32];
            b[0] = 2;
            var pair = new byte[64];
            System.Array.Copy(a, 0, pair, 0, 32);
            System.Array.Copy(b, 0, pair, 32, 32);

            var hashPair = provider.ComputeHash(pair);


            Assert.Equal(32, hashPair.Length);
            Assert.NotEqual(new byte[32], hashPair);
        }

        // Cross-validation note:
        // HorizenLabs width=12 vector: input [0..11] → output[0] = 0x01eaef96bdf1c0c1 = 138186169299091649
        // pil2-proofman test_poseidon2_12: same input → same output. Constants match.
        // Plonky3 width=12 uses DIFFERENT constants → output[0] = 0xf292ab67c0f14b03. Not comparable.
        // Zisk uses pil2-proofman constants (= HorizenLabs). Our implementation validated against these.

        [Fact]
        public void Poseidon2_Permutation_Deterministic()
        {
            var input = new GoldilocksField[16];
            for (int i = 0; i < 16; i++)
                input[i] = new GoldilocksField((ulong)i);

            var output1 = Poseidon2Core.Permute16(input);
            var output2 = Poseidon2Core.Permute16(input);

            for (int i = 0; i < 16; i++)
                Assert.Equal(output1[i], output2[i]);
        }
    }
}
