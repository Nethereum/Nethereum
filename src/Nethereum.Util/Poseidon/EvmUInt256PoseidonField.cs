using System;

namespace Nethereum.Util.Poseidon
{
    public class EvmUInt256PoseidonField : IPoseidonFieldOps<EvmUInt256>
    {
        private readonly EvmUInt256 _prime;

        public EvmUInt256PoseidonField(EvmUInt256 prime)
        {
            _prime = prime;
        }

        public EvmUInt256 Zero => EvmUInt256.Zero;

        public EvmUInt256 AddMod(EvmUInt256 a, EvmUInt256 b)
        {
            return EvmUInt256.AddMod(a, b, _prime);
        }

        public EvmUInt256 MulMod(EvmUInt256 a, EvmUInt256 b)
        {
            return EvmUInt256.MulMod(a, b, _prime);
        }

        public EvmUInt256 ModPow(EvmUInt256 baseVal, EvmUInt256 exponent)
        {
            return EvmUInt256.ModPow(baseVal % _prime, exponent, _prime);
        }

        public EvmUInt256 FromBytes(byte[] bigEndianData)
        {
            if (bigEndianData == null || bigEndianData.Length == 0)
                return EvmUInt256.Zero;
            var padded = bigEndianData.Length < 32 ? ByteUtil.PadBytes(bigEndianData, 32) : bigEndianData;
            return EvmUInt256.FromBigEndian(padded) % _prime;
        }

        public byte[] ToBytes(EvmUInt256 value)
        {
            return (value % _prime).ToBigEndian();
        }
    }
}
