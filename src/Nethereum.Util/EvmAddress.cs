using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Util
{
    public readonly struct EvmAddress : IEquatable<EvmAddress>
    {
        public const int ByteLength = 20;

        private readonly byte[] _bytes;

        private EvmAddress(byte[] bytes)
        {
            _bytes = bytes;
        }

        public static readonly EvmAddress Zero = new EvmAddress(new byte[ByteLength]);

        public static EvmAddress From(byte[] address)
        {
            if (address == null || address.Length == 0) return Zero;

            if (address.Length == ByteLength)
            {
                var exact = new byte[ByteLength];
                Array.Copy(address, exact, ByteLength);
                return new EvmAddress(exact);
            }

            var result = new byte[ByteLength];
            if (address.Length > ByteLength)
            {
                Array.Copy(address, address.Length - ByteLength, result, 0, ByteLength);
            }
            else
            {
                Array.Copy(address, 0, result, ByteLength - address.Length, address.Length);
            }
            return new EvmAddress(result);
        }

        public static EvmAddress FromHex(string address)
        {
            if (string.IsNullOrEmpty(address)) return Zero;
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
            return From(normalized.HexToByteArray());
        }

        public byte[] ToByteArray()
        {
            var copy = new byte[ByteLength];
            Array.Copy(_bytes ?? Zero._bytes, copy, ByteLength);
            return copy;
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public ReadOnlySpan<byte> AsSpan() => (_bytes ?? Zero._bytes).AsSpan();
#endif

        public string ToHexLower() => (_bytes ?? Zero._bytes).ToHex(true);

        public bool Equals(EvmAddress other)
        {
            var a = _bytes ?? Zero._bytes;
            var b = other._bytes ?? Zero._bytes;
            if (ReferenceEquals(a, b)) return true;
            for (var i = 0; i < ByteLength; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is EvmAddress other && Equals(other);

        public override int GetHashCode()
        {
            var b = _bytes ?? Zero._bytes;
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < ByteLength; i++)
                {
                    hash = hash * 31 + b[i];
                }
                return hash;
            }
        }

        public static bool operator ==(EvmAddress left, EvmAddress right) => left.Equals(right);

        public static bool operator !=(EvmAddress left, EvmAddress right) => !left.Equals(right);

        public override string ToString() => ToHexLower();
    }
}
