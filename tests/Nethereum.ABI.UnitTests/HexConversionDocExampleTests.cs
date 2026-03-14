using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class HexConversionDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "hex-encoding", "Convert byte array to hex string")]
        public void ShouldConvertBytesToHex()
        {
            var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            var hexWithPrefix = data.ToHex(prefix: true);
            var hexWithoutPrefix = data.ToHex(prefix: false);

            Assert.Equal("0xdeadbeef", hexWithPrefix);
            Assert.Equal("deadbeef", hexWithoutPrefix);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "hex-encoding", "Convert hex string to byte array")]
        public void ShouldConvertHexToBytes()
        {
            var hex = "0xdeadbeef";
            var bytes = hex.HexToByteArray();

            Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, bytes);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "hex-encoding", "Ensure hex prefix is present")]
        public void ShouldEnsureHexPrefix()
        {
            var withoutPrefix = "deadbeef";
            var withPrefix = "0xdeadbeef";

            Assert.Equal("0xdeadbeef", withoutPrefix.EnsureHexPrefix());
            Assert.Equal("0xdeadbeef", withPrefix.EnsureHexPrefix());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "hex-encoding", "Check and remove hex prefix")]
        public void ShouldCheckAndRemoveHexPrefix()
        {
            var hex = "0xdeadbeef";

            Assert.True(hex.HasHexPrefix());
            Assert.Equal("deadbeef", hex.RemoveHexPrefix());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "hex-encoding", "Compare hex strings case-insensitively")]
        public void ShouldCompareHexStrings()
        {
            var hex1 = "0xDeAdBeEf";
            var hex2 = "0xdeadbeef";

            Assert.True(hex1.IsTheSameHex(hex2));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "hex-encoding", "HexBigInteger for gas and value parameters")]
        public void ShouldCreateHexBigIntegerFromBothFormats()
        {
            var fromNumber = new HexBigInteger(new BigInteger(1_000_000));
            var fromHex = new HexBigInteger("0xf4240");

            Assert.Equal(fromNumber.Value, fromHex.Value);
            Assert.Equal("0xf4240", fromNumber.HexValue);
            Assert.Equal(new BigInteger(1_000_000), fromHex.Value);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "hex-encoding", "Encode and decode UTF-8 strings as hex")]
        public void ShouldEncodeDecodeUtf8AsHex()
        {
            var text = "Hello Ethereum";
            var hex = text.ToHexUTF8();
            var decoded = hex.HexToUTF8String();

            Assert.Equal(text, decoded);
        }
    }
}
