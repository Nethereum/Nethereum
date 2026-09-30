using System;

namespace Nethereum.CoreChain.RocksDB
{
    public static class StateValueEnvelope
    {
        public const byte CurrentVersion = 1;

        public static byte[] Encode(ReadOnlySpan<byte> payload) => Encode(CurrentVersion, payload);

        public static byte[] Encode(byte version, ReadOnlySpan<byte> payload)
        {
            var buffer = new byte[1 + payload.Length];
            buffer[0] = version;
            payload.CopyTo(buffer.AsSpan(1));
            return buffer;
        }

        public static ReadOnlySpan<byte> Decode(ReadOnlySpan<byte> data, out byte version)
        {
            if (data.Length == 0)
                throw new NotSupportedException("state value is empty (no version byte)");

            version = data[0];
            switch (version)
            {
                case 1:
                    return data.Slice(1);
                default:
                    throw new NotSupportedException($"unknown state value format version {version}");
            }
        }
    }
}
