using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Util.Poseidon
{
    public sealed class BN254PoseidonCore
    {
        private readonly BN254FieldElement[] _rcFlat;
        private readonly BN254FieldElement[] _mdsFlat;
        private readonly int _stateWidth;
        private readonly int _rate;
        private readonly int _fullRounds;
        private readonly int _partialRounds;
        private readonly int _totalRounds;

        public BN254PoseidonCore(
            BN254FieldElement[,] roundConstants,
            BN254FieldElement[,] mdsMatrix,
            int stateWidth,
            int rate,
            int fullRounds,
            int partialRounds)
        {
            _stateWidth = stateWidth;
            _rate = rate;
            _fullRounds = fullRounds;
            _partialRounds = partialRounds;
            _totalRounds = fullRounds + partialRounds;

            _rcFlat = new BN254FieldElement[_totalRounds * stateWidth];
            for (int r = 0; r < _totalRounds; r++)
                for (int c = 0; c < stateWidth; c++)
                    _rcFlat[r * stateWidth + c] = roundConstants[r, c];

            _mdsFlat = new BN254FieldElement[stateWidth * stateWidth];
            for (int r = 0; r < stateWidth; r++)
                for (int c = 0; c < stateWidth; c++)
                    _mdsFlat[r * stateWidth + c] = mdsMatrix[r, c];
        }

        public BN254FieldElement Hash(BN254FieldElement input0, BN254FieldElement input1)
        {
            var s0 = BN254FieldElement.Zero;
            var s1 = BN254FieldElement.Add(BN254FieldElement.Zero, input0);
            var s2 = BN254FieldElement.Add(BN254FieldElement.Zero, input1);

            Permute(ref s0, ref s1, ref s2);
            return s0;
        }

        public BN254FieldElement Hash(BN254FieldElement input0)
        {
            var s0 = BN254FieldElement.Zero;
            var s1 = BN254FieldElement.Add(BN254FieldElement.Zero, input0);
            var s2 = BN254FieldElement.Zero;

            Permute(ref s0, ref s1, ref s2);
            return s0;
        }

        public byte[] HashBytesToBytes(byte[] a, byte[] b)
        {
            var e0 = BN254FieldElement.FromBytes(a);
            var e1 = BN254FieldElement.FromBytes(b);
            var result = Hash(e0, e1);
            return result.ToBytes();
        }

        public byte[] HashBytesToBytes(byte[] a)
        {
            var e0 = BN254FieldElement.FromBytes(a);
            var result = Hash(e0);
            return result.ToBytes();
        }

        public byte[] Hash64BytesToBytes(byte[] data)
        {
            var e0 = BN254FieldElement.FromBytes(data, 0);
            var e1 = BN254FieldElement.FromBytes(data, 32);
            var result = Hash(e0, e1);
            return result.ToBytes();
        }

        private void Permute(ref BN254FieldElement s0, ref BN254FieldElement s1, ref BN254FieldElement s2)
        {
            var halfFull = _fullRounds / 2;
            var rc = _rcFlat;
            var mds = _mdsFlat;

            for (int round = 0; round < _totalRounds; round++)
            {
                int rcBase = round * 3;
                s0 = BN254FieldElement.Add(s0, rc[rcBase]);
                s1 = BN254FieldElement.Add(s1, rc[rcBase + 1]);
                s2 = BN254FieldElement.Add(s2, rc[rcBase + 2]);

                if (round < halfFull || round >= halfFull + _partialRounds)
                {
                    s0 = BN254FieldElement.Pow5(s0);
                    s1 = BN254FieldElement.Pow5(s1);
                    s2 = BN254FieldElement.Pow5(s2);
                }
                else
                {
                    s0 = BN254FieldElement.Pow5(s0);
                }

                var n0 = BN254FieldElement.Add(
                    BN254FieldElement.Add(
                        BN254FieldElement.Multiply(mds[0], s0),
                        BN254FieldElement.Multiply(mds[1], s1)),
                    BN254FieldElement.Multiply(mds[2], s2));
                var n1 = BN254FieldElement.Add(
                    BN254FieldElement.Add(
                        BN254FieldElement.Multiply(mds[3], s0),
                        BN254FieldElement.Multiply(mds[4], s1)),
                    BN254FieldElement.Multiply(mds[5], s2));
                var n2 = BN254FieldElement.Add(
                    BN254FieldElement.Add(
                        BN254FieldElement.Multiply(mds[6], s0),
                        BN254FieldElement.Multiply(mds[7], s1)),
                    BN254FieldElement.Multiply(mds[8], s2));

                s0 = n0;
                s1 = n1;
                s2 = n2;
            }
        }
    }
}
