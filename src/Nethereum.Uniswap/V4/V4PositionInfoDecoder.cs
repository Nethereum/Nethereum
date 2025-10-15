using System.Numerics;

namespace Nethereum.Uniswap.V4
{
    public class PositionInfoPacked
    {
        public byte[] PoolId { get; set; }
        public int TickLower { get; set; }
        public int TickUpper { get; set; }
        public bool HasSubscriber { get; set; }
    }

    public static class V4PositionInfoDecoder
    {
        private const int TICK_LOWER_OFFSET = 8;
        private const int TICK_UPPER_OFFSET = 32;
        private const int POOL_ID_OFFSET = 56;

        public static PositionInfoPacked DecodePositionInfo(BigInteger packedInfo)
        {
            var hasSubscriber = ExtractHasSubscriber(packedInfo);
            var tickLower = ExtractTickLower(packedInfo);
            var tickUpper = ExtractTickUpper(packedInfo);
            var poolId = ExtractPoolId(packedInfo);

            return new PositionInfoPacked
            {
                PoolId = poolId,
                TickLower = tickLower,
                TickUpper = tickUpper,
                HasSubscriber = hasSubscriber
            };
        }

        public static bool ExtractHasSubscriber(BigInteger packedInfo)
        {
            var mask = new BigInteger(0xFF);
            var value = packedInfo & mask;
            return value != 0;
        }

        public static int ExtractTickLower(BigInteger packedInfo)
        {
            var shifted = packedInfo >> TICK_LOWER_OFFSET;
            var mask = new BigInteger(0xFFFFFF);
            var value = shifted & mask;
            return SignExtend24Bit((int)value);
        }

        public static int ExtractTickUpper(BigInteger packedInfo)
        {
            var shifted = packedInfo >> TICK_UPPER_OFFSET;
            var mask = new BigInteger(0xFFFFFF);
            var value = shifted & mask;
            return SignExtend24Bit((int)value);
        }

        public static byte[] ExtractPoolId(BigInteger packedInfo)
        {
            var shifted = packedInfo >> POOL_ID_OFFSET;
            var poolIdBytes = shifted.ToByteArray();

            var result = new byte[25];
            int copyLength = System.Math.Min(poolIdBytes.Length, 25);
            System.Array.Copy(poolIdBytes, 0, result, 0, copyLength);

            return result;
        }

        private static int SignExtend24Bit(int value)
        {
            const int signBit = 0x800000;
            const int mask = 0xFFFFFF;

            value = value & mask;

            if ((value & signBit) != 0)
            {
                value |= unchecked((int)0xFF000000);
            }

            return value;
        }

        public static BigInteger EncodePositionInfo(byte[] poolId, int tickLower, int tickUpper, bool hasSubscriber = false)
        {
            BigInteger result = 0;

            if (hasSubscriber)
            {
                result |= 1;
            }

            var tickLowerUnsigned = (uint)(tickLower & 0xFFFFFF);
            result |= new BigInteger(tickLowerUnsigned) << TICK_LOWER_OFFSET;

            var tickUpperUnsigned = (uint)(tickUpper & 0xFFFFFF);
            result |= new BigInteger(tickUpperUnsigned) << TICK_UPPER_OFFSET;

            var poolIdPadded = new byte[poolId.Length + 1];
            System.Array.Copy(poolId, 0, poolIdPadded, 0, poolId.Length);
            poolIdPadded[poolId.Length] = 0;
            var poolIdBigInt = new BigInteger(poolIdPadded);
            result |= poolIdBigInt << POOL_ID_OFFSET;

            return result;
        }
    }
}
