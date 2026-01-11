using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.Util
{
    public class PoseidonParameters
    {
        public PoseidonParameters(
            BigInteger prime,
            int stateWidth,
            int rate,
            int capacity,
            int fullRounds,
            int partialRounds,
            int sBoxExponent,
            BigInteger[,] roundConstants,
            BigInteger[,] mdsMatrix)
        {
            if (stateWidth <= 0) throw new ArgumentOutOfRangeException(nameof(stateWidth));
            if (rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (rate + capacity != stateWidth) throw new ArgumentException("State width must equal rate + capacity");
            if (fullRounds <= 0 || fullRounds % 2 != 0)
                throw new ArgumentOutOfRangeException(nameof(fullRounds), "Full rounds must be an even number greater than zero");
            if (partialRounds <= 0) throw new ArgumentOutOfRangeException(nameof(partialRounds));
            if (roundConstants == null) throw new ArgumentNullException(nameof(roundConstants));
            if (mdsMatrix == null) throw new ArgumentNullException(nameof(mdsMatrix));

            Prime = prime;
            StateWidth = stateWidth;
            Rate = rate;
            Capacity = capacity;
            FullRounds = fullRounds;
            PartialRounds = partialRounds;
            SBoxExponent = sBoxExponent;
            RoundConstants = roundConstants;
            MdsMatrix = mdsMatrix;
        }

        public BigInteger Prime { get; }
        public int StateWidth { get; }
        public int Rate { get; }
        public int Capacity { get; }
        public int FullRounds { get; }
        public int PartialRounds { get; }
        public int SBoxExponent { get; }
        public BigInteger[,] RoundConstants { get; }
        public BigInteger[,] MdsMatrix { get; }

        /// <summary>
        /// Creates a BN254 Poseidon parameter set. By default this matches the Circom/Tornado Cash curve (t=3, R_F=8, R_P=57),
        /// but callers can supply wider rates and the appropriate round counts.
        /// </summary>
        public static PoseidonParameters CreateBn254(
            int rate = 2,
            int capacity = 1,
            int fullRounds = 8,
            int partialRounds = 57,
            int sBoxExponent = 5,
            string roundConstantSeed = PoseidonParameterFactory.CircomRoundSeed,
            string matrixSeed = PoseidonParameterFactory.CircomMdsSeed)
        {
            if (rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            var prime = BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");
            var stateWidth = rate + capacity;
            var totalRounds = fullRounds + partialRounds;

            var roundConstants = PoseidonParameterGenerator.GenerateRoundConstants(
                prime,
                stateWidth,
                totalRounds,
                roundConstantSeed);

            var mdsMatrix = PoseidonParameterGenerator.GenerateMdsMatrix(
                prime,
                stateWidth,
                matrixSeed);

            return new PoseidonParameters(
                prime,
                stateWidth,
                rate,
                capacity,
                fullRounds,
                partialRounds,
                sBoxExponent,
                roundConstants,
                mdsMatrix);
        }
    }

    internal static class PoseidonParameterGenerator
    {
        public static BigInteger[,] GenerateRoundConstants(
            BigInteger prime,
            int width,
            int rounds,
            string seed)
        {
            var constants = new BigInteger[rounds, width];
            var counter = 0;
            for (var round = 0; round < rounds; round++)
            {
                for (var column = 0; column < width; column++)
                {
                    constants[round, column] = GenerateFieldElement(
                        BuildLabel(seed, round, column, counter++),
                        prime);
                }
            }

            return constants;
        }

        public static BigInteger[,] GenerateMdsMatrix(BigInteger prime, int width, string seed)
        {
            var used = new HashSet<BigInteger>();
            var x = GenerateDistinctValues(prime, width, seed + ".x", used);
            var y = GenerateDistinctValues(prime, width, seed + ".y", used);
            var matrix = new BigInteger[width, width];

            for (var row = 0; row < width; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var denominator = Normalize(x[row] - y[col], prime);
                    if (denominator.IsZero)
                    {
                        denominator = BigInteger.One;
                    }

                    matrix[row, col] = FieldInverse(denominator, prime);
                }
            }

            return matrix;
        }

        private static BigInteger[] GenerateDistinctValues(
            BigInteger prime,
            int count,
            string seed,
            ISet<BigInteger> used)
        {
            var values = new BigInteger[count];
            var index = 0;
            var counter = 0;
            while (index < count)
            {
                var candidate = GenerateFieldElement(BuildLabel(seed, index, counter), prime);
                counter++;
                if (candidate.IsZero) continue;
                if (used.Contains(candidate)) continue;

                used.Add(candidate);
                values[index++] = candidate;
            }

            return values;
        }

        private static string BuildLabel(string seed, params int[] parts)
        {
            var builder = new StringBuilder(seed);
            foreach (var current in parts)
            {
                builder.Append('-').Append(current);
            }

            return builder.ToString();
        }

        private static BigInteger GenerateFieldElement(string label, BigInteger prime)
        {
            var digest = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(label));
            var value = BytesToPositiveInteger(digest);
            var reduced = value % prime;
            if (reduced.Sign < 0)
            {
                reduced += prime;
            }

            return reduced;
        }

        private static BigInteger BytesToPositiveInteger(byte[] bigEndianBytes)
        {
            if (bigEndianBytes == null || bigEndianBytes.Length == 0)
            {
                return BigInteger.Zero;
            }

            var reversed = new byte[bigEndianBytes.Length + 1];
            for (var i = 0; i < bigEndianBytes.Length; i++)
            {
                reversed[i] = bigEndianBytes[bigEndianBytes.Length - 1 - i];
            }

            return new BigInteger(reversed);
        }

        private static BigInteger Normalize(BigInteger value, BigInteger prime)
        {
            var result = value % prime;
            if (result.Sign < 0)
            {
                result += prime;
            }

            return result;
        }

        private static BigInteger FieldInverse(BigInteger value, BigInteger prime)
        {
            return BigInteger.ModPow(Normalize(value, prime), prime - 2, prime);
        }
    }
}
