namespace Nethereum.DevP2P.Rlpx
{
    internal static class RlpxFrameFormat
    {
        public const int BlockSize = 16;

        public static int PaddedSize(int frameSize) => ((frameSize + BlockSize - 1) / BlockSize) * BlockSize;

        public static void WriteHeaderSize(byte[] header, int frameSize)
        {
            header[0] = (byte)(frameSize >> 16);
            header[1] = (byte)(frameSize >> 8);
            header[2] = (byte)frameSize;
        }

        public static int ReadHeaderSize(byte[] header) => (header[0] << 16) | (header[1] << 8) | header[2];
    }
}
