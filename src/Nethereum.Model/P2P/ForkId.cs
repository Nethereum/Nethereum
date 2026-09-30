using System.Buffers.Binary;
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
}
