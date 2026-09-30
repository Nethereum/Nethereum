using System;
using System.IO;
using System.IO.Compression;

namespace Nethereum.CoreChain.DataAvailability
{
    public enum CompressionAlgo : byte
    {
        None = 0x00,
        Zlib = 0x01,
        Brotli = 0x02
    }

    public static class ChannelCompressor
    {
        public const int MaxFrameSize = 120_000;

        public static byte[] Compress(byte[] data, CompressionAlgo algo = CompressionAlgo.Brotli)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            switch (algo)
            {
                case CompressionAlgo.Brotli:
                    return CompressBrotli(data);
                case CompressionAlgo.Zlib:
                    return CompressZlib(data);
                case CompressionAlgo.None:
                    return data;
                default:
                    throw new ArgumentOutOfRangeException(nameof(algo));
            }
        }

        public static byte[] Decompress(byte[] data, CompressionAlgo algo)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            switch (algo)
            {
                case CompressionAlgo.Brotli:
                    return DecompressBrotli(data);
                case CompressionAlgo.Zlib:
                    return DecompressZlib(data);
                case CompressionAlgo.None:
                    return data;
                default:
                    throw new ArgumentOutOfRangeException(nameof(algo));
            }
        }

        private static byte[] CompressBrotli(byte[] data)
        {
            using var output = new MemoryStream();
            using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotli.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private static byte[] DecompressBrotli(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            brotli.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] CompressZlib(byte[] data)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private static byte[] DecompressZlib(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
    }
}
