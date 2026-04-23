using System;
using Nethereum.Util.Poseidon;

namespace Nethereum.Util.HashProviders
{
    public class BN254PoseidonPairHashProvider : IHashProvider
    {
        private readonly BN254PoseidonCore _core;

        public BN254PoseidonPairHashProvider()
        {
            var preset = PoseidonPrecomputedConstants.GetPreset(PoseidonParameterPreset.CircomT2);

            var rc = new BN254FieldElement[preset.RoundConstants.GetLength(0), preset.RoundConstants.GetLength(1)];
            for (int r = 0; r < rc.GetLength(0); r++)
                for (int c = 0; c < rc.GetLength(1); c++)
                    rc[r, c] = BN254FieldElement.FromEvmUInt256(preset.RoundConstants[r, c]);

            var mds = new BN254FieldElement[preset.MdsMatrix.GetLength(0), preset.MdsMatrix.GetLength(1)];
            for (int r = 0; r < mds.GetLength(0); r++)
                for (int c = 0; c < mds.GetLength(1); c++)
                    mds[r, c] = BN254FieldElement.FromEvmUInt256(preset.MdsMatrix[r, c]);

            _core = new BN254PoseidonCore(
                rc, mds,
                preset.StateWidth, preset.Rate, preset.FullRounds, preset.PartialRounds);
        }

        public byte[] ComputeHash(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length == 32)
                return _core.HashBytesToBytes(data);

            if (data.Length == 64)
                return _core.Hash64BytesToBytes(data);

            throw new ArgumentException(
                $"BN254PoseidonPairHashProvider expects 32 or 64 bytes, got {data.Length}",
                nameof(data));
        }
    }
}
