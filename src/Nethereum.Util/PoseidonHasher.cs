using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Util
{
    public class PoseidonHasher
    {
        private readonly PoseidonParameters _parameters;
        private readonly BigInteger _prime;
        private readonly int _totalRounds;

        public PoseidonHasher()
            : this(null)
        {
        }

        public PoseidonHasher(PoseidonParameterPreset preset)
            : this(PoseidonParameterFactory.GetPreset(preset))
        {
        }

        public PoseidonHasher(PoseidonParameters parameters)
        {
            _parameters = parameters ?? PoseidonParameterFactory.GetPreset(PoseidonParameterFactory.DefaultPreset);
            _prime = _parameters.Prime;
            _totalRounds = _parameters.FullRounds + _parameters.PartialRounds;
        }

        public BigInteger Hash(params BigInteger[] inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var state = new BigInteger[_parameters.StateWidth];
            var normalizedInputs = inputs.Select(Normalize).ToArray();
            var capacityOffset = _parameters.StateWidth - _parameters.Rate;
            var rateIndex = 0;
            var absorbedAny = normalizedInputs.Length > 0;

            foreach (var element in normalizedInputs)
            {
                var stateIndex = capacityOffset + rateIndex;
                state[stateIndex] = FieldAdd(state[stateIndex], element);
                rateIndex++;
                if (rateIndex == _parameters.Rate)
                {
                    state = Permute(state);
                    rateIndex = 0;
                }
            }

            if (rateIndex > 0 || !absorbedAny)
            {
                state = Permute(state);
            }

            return state[0];
        }

        public byte[] HashToBytes(params BigInteger[] inputs)
        {
            var fieldElement = Hash(inputs);
            return ToBigEndianBytes(fieldElement);
        }

        /// <summary>
        /// Hashes one or more big-endian byte slices by converting each slice into a field element.
        /// </summary>
        public BigInteger HashBytes(params byte[][] inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var elements = inputs.Select(ConvertBytesToFieldElement).ToArray();
            return Hash(elements);
        }

        /// <summary>
        /// Hashes one or more big-endian hex slices (with or without a 0x prefix).
        /// </summary>
        public BigInteger HashHex(params string[] hexInputs)
        {
            if (hexInputs == null) throw new ArgumentNullException(nameof(hexInputs));

            var elements = hexInputs.Select(hex => ConvertBytesToFieldElement((hex ?? string.Empty).HexToByteArray())).ToArray();
            return Hash(elements);
        }

        /// <summary>
        /// Produces a 32-byte big-endian digest from the supplied big-endian byte slices.
        /// </summary>
        public byte[] HashBytesToBytes(params byte[][] inputs)
        {
            var fieldElement = HashBytes(inputs);
            return ToBigEndianBytes(fieldElement);
        }

        private BigInteger[] Permute(BigInteger[] state)
        {
            var working = (BigInteger[])state.Clone();
            var halfFullRounds = _parameters.FullRounds / 2;

            for (var round = 0; round < _totalRounds; round++)
            {
                ApplyRoundConstants(working, round);
                ApplySBoxLayer(working, halfFullRounds, round);
                working = MultiplyByMds(working);
            }

            return working;
        }

        private void ApplyRoundConstants(IList<BigInteger> state, int round)
        {
            for (var index = 0; index < state.Count; index++)
            {
                state[index] = FieldAdd(state[index], _parameters.RoundConstants[round, index]);
            }
        }

        private void ApplySBoxLayer(IList<BigInteger> state, int halfFullRounds, int round)
        {
            var isFullRound = round < halfFullRounds || round >= halfFullRounds + _parameters.PartialRounds;
            if (isFullRound)
            {
                for (var index = 0; index < state.Count; index++)
                {
                    state[index] = ApplySBox(state[index]);
                }
            }
            else
            {
                state[0] = ApplySBox(state[0]);
            }
        }

        private BigInteger[] MultiplyByMds(IReadOnlyList<BigInteger> state)
        {
            var next = new BigInteger[_parameters.StateWidth];
            for (var row = 0; row < _parameters.StateWidth; row++)
            {
                var accumulator = BigInteger.Zero;
                for (var column = 0; column < _parameters.StateWidth; column++)
                {
                    var term = FieldMul(_parameters.MdsMatrix[row, column], state[column]);
                    accumulator = FieldAdd(accumulator, term);
                }

                next[row] = accumulator;
            }

            return next;
        }

        private BigInteger ApplySBox(BigInteger value)
        {
            var exponent = _parameters.SBoxExponent;
            return BigInteger.ModPow(Normalize(value), exponent, _prime);
        }

        private BigInteger FieldAdd(BigInteger left, BigInteger right)
        {
            return Normalize(left + right);
        }

        private BigInteger FieldMul(BigInteger left, BigInteger right)
        {
            return Normalize(left * right);
        }

        private BigInteger Normalize(BigInteger value)
        {
            var result = value % _prime;
            if (result.Sign < 0)
            {
                result += _prime;
            }

            return result;
        }

        private BigInteger ConvertBytesToFieldElement(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return BigInteger.Zero;
            }

            var unsigned = new byte[bytes.Length + 1];
            for (var i = 0; i < bytes.Length; i++)
            {
                unsigned[i] = bytes[bytes.Length - 1 - i];
            }

            return Normalize(new BigInteger(unsigned));
        }

        private byte[] ToBigEndianBytes(BigInteger value)
        {
            var normalized = Normalize(value);
            var littleEndian = normalized.ToByteArray();
            var reversed = new byte[littleEndian.Length];
            for (var i = 0; i < littleEndian.Length; i++)
            {
                reversed[i] = littleEndian[littleEndian.Length - 1 - i];
            }

            var firstNonZero = Array.FindIndex(reversed, b => b != 0);
            byte[] bigEndian;
            if (firstNonZero == -1)
            {
                bigEndian = new byte[] { 0x00 };
            }
            else
            {
                var length = reversed.Length - firstNonZero;
                bigEndian = new byte[length];
                Array.Copy(reversed, firstNonZero, bigEndian, 0, length);
            }

            if (bigEndian.Length >= 32)
            {
                return bigEndian;
            }

            var padded = new byte[32];
            Array.Copy(bigEndian, 0, padded, 32 - bigEndian.Length, bigEndian.Length);
            return padded;
        }
    }
}
