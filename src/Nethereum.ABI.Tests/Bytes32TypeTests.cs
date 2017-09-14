using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.Tests
{
    public class Bytes32EncodingTests
    {
        [Fact]
        public virtual void Encode_33ByteString_ThrowsArgumentException()
        {
            Bytes32Type bytes32Type = new Bytes32Type("bytes32");
            Assert.Throws<ArgumentException>(() => bytes32Type.Encode("123456789012345612345678901234561"));
        }

        [Fact]
        public virtual void Encode_32ByteString_Returns32CorrectBytes()
        {
            Bytes32Type bytes32Type = new Bytes32Type("bytes32");
            var result = bytes32Type.Encode("12345678901234561234567890123456").ToHex();
            Assert.Equal("3132333435363738393031323334353631323334353637383930313233343536", result);
        }

        [Fact]
        public virtual void Encode_15ByteString_Returns32PaddedBytes()
        {
            Bytes32Type bytes32Type = new Bytes32Type("bytes32");
            var result = bytes32Type.Encode("123456789012345").ToHex();
            Console.WriteLine(result);
            
            Assert.Equal("3132333435363738393031323334350000000000000000000000000000000000", result);
        }
        
    }
}