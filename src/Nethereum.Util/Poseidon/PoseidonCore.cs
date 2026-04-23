using System;

namespace Nethereum.Util.Poseidon
{
    public class PoseidonCore<T>
    {
        private readonly IPoseidonFieldOps<T> _field;
        private readonly T[,] _roundConstants;
        private readonly T[,] _mdsMatrix;
        private readonly int _stateWidth;
        private readonly int _rate;
        private readonly int _fullRounds;
        private readonly int _partialRounds;
        private readonly int _totalRounds;
        private readonly T _sBoxExponent;

        public PoseidonCore(
            IPoseidonFieldOps<T> field,
            T[,] roundConstants,
            T[,] mdsMatrix,
            int stateWidth,
            int rate,
            int fullRounds,
            int partialRounds,
            T sBoxExponent)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _roundConstants = roundConstants ?? throw new ArgumentNullException(nameof(roundConstants));
            _mdsMatrix = mdsMatrix ?? throw new ArgumentNullException(nameof(mdsMatrix));
            _stateWidth = stateWidth;
            _rate = rate;
            _fullRounds = fullRounds;
            _partialRounds = partialRounds;
            _totalRounds = fullRounds + partialRounds;
            _sBoxExponent = sBoxExponent;
        }

        public T Hash(params T[] inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var state = new T[_stateWidth];
            for (int i = 0; i < _stateWidth; i++)
                state[i] = _field.Zero;

            var capacityOffset = _stateWidth - _rate;
            var rateIndex = 0;
            var absorbedAny = inputs.Length > 0;

            for (int i = 0; i < inputs.Length; i++)
            {
                var stateIndex = capacityOffset + rateIndex;
                state[stateIndex] = _field.AddMod(state[stateIndex], inputs[i]);
                rateIndex++;
                if (rateIndex == _rate)
                {
                    Permute(state);
                    rateIndex = 0;
                }
            }

            if (rateIndex > 0 || !absorbedAny)
            {
                Permute(state);
            }

            return state[0];
        }

        public byte[] HashBytesToBytes(params byte[][] inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var elements = new T[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
                elements[i] = _field.FromBytes(inputs[i]);

            var result = Hash(elements);
            return _field.ToBytes(result);
        }

        private void Permute(T[] state)
        {
            var halfFullRounds = _fullRounds / 2;
            var next = new T[_stateWidth];

            for (var round = 0; round < _totalRounds; round++)
            {
                for (int i = 0; i < _stateWidth; i++)
                    state[i] = _field.AddMod(state[i], _roundConstants[round, i]);

                var isFullRound = round < halfFullRounds || round >= halfFullRounds + _partialRounds;
                if (isFullRound)
                {
                    for (int i = 0; i < _stateWidth; i++)
                        state[i] = _field.ModPow(state[i], _sBoxExponent);
                }
                else
                {
                    state[0] = _field.ModPow(state[0], _sBoxExponent);
                }

                for (int row = 0; row < _stateWidth; row++)
                {
                    var acc = _field.Zero;
                    for (int col = 0; col < _stateWidth; col++)
                    {
                        var term = _field.MulMod(_mdsMatrix[row, col], state[col]);
                        acc = _field.AddMod(acc, term);
                    }
                    next[row] = acc;
                }
                Array.Copy(next, state, _stateWidth);
            }
        }
    }
}
