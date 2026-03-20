using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    public class Blake3KnownVectorTests
    {
        private static byte[] MakeInput(int length)
        {
            var input = new byte[length];
            for (int i = 0; i < length; i++)
                input[i] = (byte)(i % 251);
            return input;
        }

        private readonly Blake3HashProvider _blake3 = new Blake3HashProvider();

        [Theory]
        [Trait("Category", "Blake3-Vectors")]
        [InlineData(0, "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262")]
        [InlineData(1, "2d3adedff11b61f14c886e35afa036736dcd87a74d27b5c1510225d0f592e213")]
        [InlineData(2, "7b7015bb92cf0b318037702a6cdd81dee41224f734684c2c122cd6359cb1ee63")]
        [InlineData(3, "e1be4d7a8ab5560aa4199eea339849ba8e293d55ca0a81006726d184519e647f")]
        [InlineData(4, "f30f5ab28fe047904037f77b6da4fea1e27241c5d132638d8bedce9d40494f32")]
        [InlineData(5, "b40b44dfd97e7a84a996a91af8b85188c66c126940ba7aad2e7ae6b385402aa2")]
        [InlineData(6, "06c4e8ffb6872fad96f9aaca5eee1553eb62aed0ad7198cef42e87f6a616c844")]
        [InlineData(7, "3f8770f387faad08faa9d8414e9f449ac68e6ff0417f673f602a646a891419fe")]
        [InlineData(8, "2351207d04fc16ade43ccab08600939c7c1fa70a5c0aaca76063d04c3228eaeb")]
        [InlineData(63, "e9bc37a594daad83be9470df7f7b3798297c3d834ce80ba85d6e207627b7db7b")]
        [InlineData(64, "4eed7141ea4a5cd4b788606bd23f46e212af9cacebacdc7d1f4c6dc7f2511b98")]
        [InlineData(65, "de1e5fa0be70df6d2be8fffd0e99ceaa8eb6e8c93a63f2d8d1c30ecb6b263dee")]
        [InlineData(127, "d81293fda863f008c09e92fc382a81f5a0b4a1251cba1634016a0f86a6bd640d")]
        [InlineData(128, "f17e570564b26578c33bb7f44643f539624b05df1a76c81f30acd548c44b45ef")]
        [InlineData(129, "683aaae9f3c5ba37eaaf072aed0f9e30bac0865137bae68b1fde4ca2aebdcb12")]
        [InlineData(1023, "10108970eeda3eb932baac1428c7a2163b0e924c9a9e25b35bba72b28f70bd11")]
        [InlineData(1024, "42214739f095a406f3fc83deb889744ac00df831c10daa55189b5d121c855af7")]
        public void Blake3_OfficialTestVectors(int inputLen, string expectedHex)
        {
            var input = MakeInput(inputLen);
            var result = _blake3.ComputeHash(input);
            var expected = expectedHex.HexToByteArray();

            Assert.Equal(32, result.Length);
            Assert.Equal(expected, result);
        }

        [Fact]
        [Trait("Category", "Blake3-Vectors")]
        public void Blake3_EmptyInput_MatchesKnownHash()
        {
            var result = _blake3.ComputeHash(Array.Empty<byte>());
            var expected = "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262".HexToByteArray();
            Assert.Equal(expected, result);
        }

        [Fact]
        [Trait("Category", "Blake3-Vectors")]
        public void Blake3_Returns32Bytes()
        {
            var result = _blake3.ComputeHash(new byte[] { 0x01, 0x02, 0x03 });
            Assert.Equal(32, result.Length);
        }

        [Fact]
        [Trait("Category", "Blake3-Vectors")]
        public void Blake3_Deterministic()
        {
            var data = new byte[] { 0x01, 0x02, 0x03 };
            Assert.Equal(_blake3.ComputeHash(data), _blake3.ComputeHash(data));
        }

        [Fact]
        [Trait("Category", "Blake3-Vectors")]
        public void Blake3_DifferentInputs_DifferentHashes()
        {
            var hash1 = _blake3.ComputeHash(new byte[] { 0x01 });
            var hash2 = _blake3.ComputeHash(new byte[] { 0x02 });
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        [Trait("Category", "Blake3-Vectors")]
        public void Blake3_DiffersFromSha256()
        {
            var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var blake3 = _blake3.ComputeHash(data);
            var sha256 = new Nethereum.Util.HashProviders.Sha256HashProvider().ComputeHash(data);
            Assert.NotEqual(blake3, sha256);
        }
    }
}
