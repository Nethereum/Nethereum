using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.Poseidon;

namespace Nethereum.Util
{
    public class PoseidonHasher
    {
        private readonly PoseidonCore<BigInteger> _core;
        private readonly BigIntegerPoseidonField _field;

        public PoseidonHasher()
            : this((PoseidonParameters)null)
        {
        }

        public PoseidonHasher(PoseidonParameterPreset preset)
            : this(PoseidonParameterFactory.GetPreset(preset))
        {
        }

        public PoseidonHasher(PoseidonParameters parameters)
        {
            parameters = parameters ?? PoseidonParameterFactory.GetPreset(PoseidonParameterFactory.DefaultPreset);
            _field = new BigIntegerPoseidonField(parameters.Prime);
            _core = new PoseidonCore<BigInteger>(
                _field,
                parameters.RoundConstants,
                parameters.MdsMatrix,
                parameters.StateWidth,
                parameters.Rate,
                parameters.FullRounds,
                parameters.PartialRounds,
                (BigInteger)parameters.SBoxExponent);
        }

        public BigInteger Hash(params BigInteger[] inputs)
        {
            return _core.Hash(inputs);
        }

        public byte[] HashToBytes(params BigInteger[] inputs)
        {
            return _field.ToBytes(Hash(inputs));
        }

        public BigInteger HashBytes(params byte[][] inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var elements = new BigInteger[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
                elements[i] = _field.FromBytes(inputs[i]);
            return _core.Hash(elements);
        }

        public BigInteger HashHex(params string[] hexInputs)
        {
            if (hexInputs == null) throw new ArgumentNullException(nameof(hexInputs));
            var elements = new BigInteger[hexInputs.Length];
            for (int i = 0; i < hexInputs.Length; i++)
                elements[i] = _field.FromBytes((hexInputs[i] ?? string.Empty).HexToByteArray());
            return _core.Hash(elements);
        }

        public byte[] HashBytesToBytes(params byte[][] inputs)
        {
            return _core.HashBytesToBytes(inputs);
        }
    }
}
