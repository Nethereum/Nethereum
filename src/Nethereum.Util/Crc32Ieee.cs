namespace Nethereum.Util
{
    public static class Crc32Ieee
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                table[i] = crc;
            }
            return table;
        }

        public static uint Update(uint crc, byte[] data)
        {
            foreach (byte b in data)
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        public static uint Compute(byte[] data)
        {
            return Update(0xFFFFFFFFu, data) ^ 0xFFFFFFFFu;
        }
    }
}
