using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class BytesTests
    {

        [Fact]
        public virtual void ShouldEncodeBytes()
        {
            var bytesType = ABIType.CreateABIType("bytes");
            var bytes = new byte[] { };
            var result = bytesType.Encode(bytes);
            //just the length
            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000000", result.ToHex(true));
            bytes = new byte[] { 0x01 };
            result = bytesType.Encode(bytes);
            Assert.Equal("0x00000000000000000000000000000000000000000000000000000000000000010100000000000000000000000000000000000000000000000000000000000000", result.ToHex(true));

            bytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x00, 0x01, 0x02, 0x03, 0x04 };

            result = bytesType.Encode(bytes);
            Assert.Equal("0x000000000000000000000000000000000000000000000000000000000000002201020304050607080900010203040506070809000102030405060708090001020304000000000000000000000000000000000000000000000000000000000000", result.ToHex(true));
        }
    }
}