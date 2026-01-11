using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.Util
{
    internal static class PoseidonCircomParameterGenerator
    {
        private const int FieldIndicator = 1;
        private const int SBoxIndicator = 0;
        private const int FieldSize = 254;
        private const int FullRounds = 8;
        private const int Capacity = 1;
        private const int SBoxExponent = 5;
        private static readonly BigInteger Prime =
            BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");

        public static PoseidonParameters Generate(int stateWidth, int partialRounds)
        {
            var grain = new GrainSequence(
                FieldIndicator,
                SBoxIndicator,
                FieldSize,
                stateWidth,
                FullRounds,
                partialRounds,
                Prime);

            var roundConstants = GenerateRoundConstants(grain, stateWidth, partialRounds);
            var mdsMatrix = GenerateMdsMatrix(grain, stateWidth);

            return new PoseidonParameters(
                Prime,
                stateWidth,
                stateWidth - Capacity,
                Capacity,
                FullRounds,
                partialRounds,
                SBoxExponent,
                roundConstants,
                mdsMatrix);
        }

        private static BigInteger[,] GenerateRoundConstants(GrainSequence grain, int stateWidth, int partialRounds)
        {
            var totalRounds = FullRounds + partialRounds;
            var constants = new BigInteger[totalRounds, stateWidth];
            for (var round = 0; round < totalRounds; round++)
            {
                for (var column = 0; column < stateWidth; column++)
                {
                    constants[round, column] = grain.NextFieldElement();
                }
            }

            return constants;
        }

        private static BigInteger[,] GenerateMdsMatrix(GrainSequence grain, int stateWidth)
        {
            while (true)
            {
                var elements = GenerateDistinctElements(grain, stateWidth * 2);
                var xs = elements.Take(stateWidth).ToArray();
                var ys = elements.Skip(stateWidth).ToArray();
                var matrix = new BigInteger[stateWidth, stateWidth];
                var valid = true;

                for (var row = 0; row < stateWidth && valid; row++)
                {
                    for (var col = 0; col < stateWidth; col++)
                    {
                        var sum = FieldAdd(xs[row], ys[col]);
                        if (sum.IsZero)
                        {
                            valid = false;
                            break;
                        }

                        matrix[row, col] = FieldInverse(sum);
                    }
                }

                if (valid)
                {
                    return matrix;
                }
            }
        }

        private static BigInteger[] GenerateDistinctElements(GrainSequence grain, int count)
        {
            while (true)
            {
                var values = new BigInteger[count];
                var seen = new HashSet<BigInteger>();
                var unique = true;
                for (var index = 0; index < count; index++)
                {
                    var candidate = grain.NextMatrixElement();
                    values[index] = candidate;
                    if (!seen.Add(candidate))
                    {
                        unique = false;
                    }
                }

                if (unique)
                {
                    return values;
                }
            }
        }

        private static BigInteger FieldAdd(BigInteger left, BigInteger right)
        {
            return Normalize(left + right);
        }

        private static BigInteger FieldInverse(BigInteger value)
        {
            return BigInteger.ModPow(Normalize(value), Prime - 2, Prime);
        }

        private static BigInteger Normalize(BigInteger value)
        {
            var reduced = value % Prime;
            if (reduced.Sign < 0)
            {
                reduced += Prime;
            }

            return reduced;
        }

        private sealed class GrainSequence
        {
            private readonly int _fieldSize;
            private readonly BigInteger _prime;
            private readonly int[] _state;

            public GrainSequence(
                int field,
                int sbox,
                int fieldSize,
                int numCells,
                int fullRounds,
                int partialRounds,
                BigInteger prime)
            {
                _fieldSize = fieldSize;
                _prime = prime;
                _state = BuildInitialState(field, sbox, fieldSize, numCells, fullRounds, partialRounds);
                for (var i = 0; i < 160; i++)
                {
                    StepRaw();
                }
            }

            public BigInteger NextFieldElement()
            {
                BigInteger candidate;
                do
                {
                    candidate = NextBits(_fieldSize);
                } while (candidate >= _prime);

                return candidate;
            }

            public BigInteger NextMatrixElement()
            {
                return Normalize(NextBits(_fieldSize));
            }

            private BigInteger NextBits(int count)
            {
                var value = BigInteger.Zero;
                for (var i = 0; i < count; i++)
                {
                    value <<= 1;
                    value |= NextBit();
                }

                return value;
            }

            private int NextBit()
            {
                var newBit = StepRaw();
                while (newBit == 0)
                {
                    newBit = StepRaw();
                    newBit = StepRaw();
                }

                newBit = StepRaw();
                return newBit;
            }

            private int StepRaw()
            {
                var newBit = _state[62]
                             ^ _state[51]
                             ^ _state[38]
                             ^ _state[23]
                             ^ _state[13]
                             ^ _state[0];

                Array.Copy(_state, 1, _state, 0, _state.Length - 1);
                _state[_state.Length - 1] = newBit;
                return newBit;
            }

            private static int[] BuildInitialState(
                int field,
                int sbox,
                int fieldSize,
                int numCells,
                int fullRounds,
                int partialRounds)
            {
                var sequence = new List<int>();
                sequence.AddRange(ToBits(field, 2));
                sequence.AddRange(ToBits(sbox, 4));
                sequence.AddRange(ToBits(fieldSize, 12));
                sequence.AddRange(ToBits(numCells, 12));
                sequence.AddRange(ToBits(fullRounds, 10));
                sequence.AddRange(ToBits(partialRounds, 10));
                sequence.AddRange(Enumerable.Repeat(1, 30));
                return sequence.ToArray();
            }

            private static IEnumerable<int> ToBits(int value, int width)
            {
                var binary = Convert.ToString(value, 2).PadLeft(width, '0');
                foreach (var character in binary)
                {
                    yield return character == '1' ? 1 : 0;
                }
            }
        }
    }
}
