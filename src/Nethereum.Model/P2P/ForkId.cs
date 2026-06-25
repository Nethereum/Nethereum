using System.Buffers.Binary;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Model.P2P
{
    public static class ForkId
    {
        public static uint ComputeHash(byte[] genesisHash, ulong[] pastForkBlocksOrTimestamps)
        {
            uint crc = Crc32Ieee.Update(0xFFFFFFFFu, genesisHash);

            foreach (var fork in pastForkBlocksOrTimestamps)
            {
                var buf = new byte[8];
                BinaryPrimitives.WriteUInt64BigEndian(buf, fork);
                crc = Crc32Ieee.Update(crc, buf);
            }

            return crc ^ 0xFFFFFFFFu;
        }
    }

    public static class MainnetForks
    {
        public static readonly byte[] GenesisHash =
            "d4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3"
            .HexToByteArray();

        public static readonly ulong[] BlockForks = {
            1_150_000,
            1_920_000,
            2_463_000,
            2_675_000,
            4_370_000,
            7_280_000,
            9_069_000,
            9_200_000,
            12_244_000,
            12_965_000,
            13_773_000,
            15_050_000,
        };

        public static readonly ulong[] TimestampForks = {
            1_681_338_455,
            1_710_338_135,
            1_746_612_311,
            1_764_798_551,
        };
    }
}
