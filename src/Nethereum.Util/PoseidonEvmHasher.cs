using System;
using Nethereum.Util.Poseidon;

namespace Nethereum.Util
{
    public class PoseidonEvmHasher
    {
        private readonly PoseidonCore<EvmUInt256> _core;

        public PoseidonEvmHasher()
            : this(PoseidonParameterPreset.CircomT3)
        {
        }

        public PoseidonEvmHasher(PoseidonParameterPreset preset)
        {
            var p = PoseidonPrecomputedConstants.GetPreset(preset);
            var field = new EvmUInt256PoseidonField(PoseidonPrecomputedConstants.Prime);
            _core = new PoseidonCore<EvmUInt256>(
                field, p.RoundConstants, p.MdsMatrix,
                p.StateWidth, p.Rate, p.FullRounds, p.PartialRounds,
                (EvmUInt256)p.SBoxExponent);
        }

        public EvmUInt256 Hash(params EvmUInt256[] inputs)
        {
            return _core.Hash(inputs);
        }

        public byte[] HashBytesToBytes(params byte[][] inputs)
        {
            return _core.HashBytesToBytes(inputs);
        }
    }
}
