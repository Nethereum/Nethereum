using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.Tests
{
    public class Bytes16EncodingTests
    {
        [Fact]
        public virtual void Encode_17ByteString_ThrowsArgumentException()
        {
            Bytes16Type bytes16Type = new Bytes16Type("bytes16");
            Assert.Throws<ArgumentException>(() => bytes16Type.Encode("12345678901234567"));
        }

        [Fact]
        public virtual void Encode_16ByteString_Returns16CorrectBytes()
        {
            Bytes16Type bytes16Type = new Bytes16Type("bytes16");
            var result = bytes16Type.Encode("1234567890123456").ToHex();
            Assert.Equal("31323334353637383930313233343536", result);
        }

        [Fact]
        public virtual void Encode_15ByteString_Returns16PaddedBytes()
        {
            Bytes16Type bytes16Type = new Bytes16Type("bytes16");
            var result = bytes16Type.Encode("123456789012345").ToHex();
            Assert.Equal("31323334353637383930313233343500", result);
        }
    }
}