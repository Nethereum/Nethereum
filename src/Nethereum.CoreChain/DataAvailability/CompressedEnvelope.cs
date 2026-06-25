using System;

namespace Nethereum.CoreChain.DataAvailability
{
    public static class CompressedEnvelope
    {
        public const byte CurrentVersion = 1;
        public const int HeaderSize = 2;

        public static byte[] Wrap(byte[] data, CompressionAlgo compression)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            var compressed = ChannelCompressor.Compress(data, compression);
            var envelope = new byte[HeaderSize + compressed.Length];
            envelope[0] = CurrentVersion;
            envelope[1] = (byte)compression;
            Array.Copy(compressed, 0, envelope, HeaderSize, compressed.Length);
            return envelope;
        }

        public static byte[] Unwrap(byte[] envelope)
        {
            if (envelope == null || envelope.Length < HeaderSize)
                return Array.Empty<byte>();

            var version = envelope[0];
            if (version != CurrentVersion)
                throw new NotSupportedException($"Unsupported envelope version: {version}");

            var algo = (CompressionAlgo)envelope[1];
            var compressed = new byte[envelope.Length - HeaderSize];
            Array.Copy(envelope, HeaderSize, compressed, 0, compressed.Length);
            return ChannelCompressor.Decompress(compressed, algo);
        }
    }
}
