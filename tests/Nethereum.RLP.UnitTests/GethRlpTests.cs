using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.RLP.UnitTests
{
    public class GethRlpTests
    {
        [Theory]
        [MemberData(nameof(GethRlpTestVectors.GetStringEncodingTests), MemberType = typeof(GethRlpTestVectors))]
        public void StringEncoding_GethTestVectors(string name, string input, string expectedOutput)
        {
            byte[] testBytes;
            if (input.Length == 1 && input[0] < 128)
            {
                testBytes = new byte[] { (byte)input[0] };
            }
            else
            {
                testBytes = Encoding.UTF8.GetBytes(input);
            }

            var encodeResult = RLP.EncodeElement(testBytes);
            Assert.Equal(expectedOutput.ToLower(), encodeResult.ToHex().ToLower());
        }

        [Theory]
        [MemberData(nameof(GethRlpTestVectors.GetIntegerEncodingTests), MemberType = typeof(GethRlpTestVectors))]
        public void IntegerEncoding_GethTestVectors(string name, BigInteger input, string expectedOutput)
        {
            var testBytes = input.ToBytesForRLPEncoding();
            var encodeResult = RLP.EncodeElement(testBytes);
            Assert.Equal(expectedOutput.ToLower(), encodeResult.ToHex().ToLower());
        }

        [Theory]
        [MemberData(nameof(GethRlpTestVectors.GetListEncodingTests), MemberType = typeof(GethRlpTestVectors))]
        public void ListEncoding_GethTestVectors(string name, string[] input, string expectedOutput)
        {
            var encodedElements = new List<byte[]>();
            foreach (var item in input)
            {
                encodedElements.Add(RLP.EncodeElement(Encoding.UTF8.GetBytes(item)));
            }

            var encodeResult = RLP.EncodeList(encodedElements.ToArray());
            Assert.Equal(expectedOutput.ToLower(), encodeResult.ToHex().ToLower());
        }

        [Fact]
        public void ListsOfLists_GethTestVector()
        {
            var innerList = RLP.EncodeList(new[] { RLP.EncodeList(new byte[0][]), RLP.EncodeList(new byte[0][]) });
            var outerList = RLP.EncodeList(new[] { innerList, RLP.EncodeList(new byte[0][]) });

            Assert.Equal("c4c2c0c0c0", outerList.ToHex().ToLower());
        }

        [Fact]
        public void ListsOfLists2_GethTestVector()
        {
            var emptyList = RLP.EncodeList(new byte[0][]);
            var listWithEmpty = RLP.EncodeList(new[] { emptyList });
            var nestedList = RLP.EncodeList(new[] { emptyList, listWithEmpty });
            var result = RLP.EncodeList(new[] { emptyList, listWithEmpty, nestedList });

            Assert.Equal("c7c0c1c0c3c0c1c0", result.ToHex().ToLower());
        }

        [Fact]
        public void MultiList_GethTestVector()
        {
            var zw = RLP.EncodeElement(Encoding.UTF8.GetBytes("zw"));
            var four = RLP.EncodeElement(new BigInteger(4).ToBytesForRLPEncoding());
            var fourList = RLP.EncodeList(new[] { four });
            var one = RLP.EncodeElement(new BigInteger(1).ToBytesForRLPEncoding());
            var result = RLP.EncodeList(new[] { zw, fourList, one });

            Assert.Equal("c6827a77c10401", result.ToHex().ToLower());
        }

        [Fact]
        public void LongString2_GethTestVector()
        {
            var testBytes = Encoding.UTF8.GetBytes(GethRlpTestVectors.LongString2Input);
            var encodeResult = RLP.EncodeElement(testBytes);

            Assert.Equal(GethRlpTestVectors.LongString2Expected.ToLower(), encodeResult.ToHex().ToLower());
        }

        [Theory]
        [MemberData(nameof(GethRlpTestVectors.GetLongListTests), MemberType = typeof(GethRlpTestVectors))]
        public void LongList_GethTestVectors(string name, string expectedOutput)
        {
            var sublist = new List<byte[]>();
            sublist.Add(RLP.EncodeElement(Encoding.UTF8.GetBytes("asdf")));
            sublist.Add(RLP.EncodeElement(Encoding.UTF8.GetBytes("qwer")));
            sublist.Add(RLP.EncodeElement(Encoding.UTF8.GetBytes("zxcv")));
            var encodedSublist = RLP.EncodeList(sublist.ToArray());

            var count = name == "longList1" ? 4 : 32;
            var mainList = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                mainList.Add(encodedSublist);
            }

            var result = RLP.EncodeList(mainList.ToArray());
            Assert.Equal(expectedOutput.ToLower(), result.ToHex().ToLower());
        }

        [Fact]
        public void DictTest1_GethTestVector()
        {
            var pairs = new List<byte[]>();
            for (int i = 1; i <= 4; i++)
            {
                var key = RLP.EncodeElement(Encoding.UTF8.GetBytes($"key{i}"));
                var val = RLP.EncodeElement(Encoding.UTF8.GetBytes($"val{i}"));
                pairs.Add(RLP.EncodeList(new[] { key, val }));
            }

            var result = RLP.EncodeList(pairs.ToArray());
            Assert.Equal("ecca846b6579318476616c31ca846b6579328476616c32ca846b6579338476616c33ca846b6579348476616c34", result.ToHex().ToLower());
        }

        [Fact]
        public void Decode_StringList_ShouldMatchOriginal()
        {
            var encoded = "cc83646f6783676f6483636174".HexToByteArray();
            var decoded = RLP.Decode(encoded) as RLPCollection;

            Assert.NotNull(decoded);
            Assert.Equal(3, decoded.Count);
            Assert.Equal("dog", Encoding.UTF8.GetString(decoded[0].RLPData));
            Assert.Equal("god", Encoding.UTF8.GetString(decoded[1].RLPData));
            Assert.Equal("cat", Encoding.UTF8.GetString(decoded[2].RLPData));
        }

        [Fact]
        public void Decode_NestedList_ShouldMatchStructure()
        {
            var encoded = "c7c0c1c0c3c0c1c0".HexToByteArray();
            var decoded = RLP.Decode(encoded) as RLPCollection;

            Assert.NotNull(decoded);
            Assert.Equal(3, decoded.Count);
            Assert.IsType<RLPCollection>(decoded[0]);
            Assert.Equal(0, ((RLPCollection)decoded[0]).Count);
        }

        [Theory]
        [InlineData("80", null)]
        [InlineData("01", new byte[] { 0x01 })]
        [InlineData("7f", new byte[] { 0x7f })]
        [InlineData("8180", new byte[] { 0x80 })]
        [InlineData("8203e8", new byte[] { 0x03, 0xe8 })]
        public void Decode_Integers_ShouldRoundTrip(string encoded, byte[] expectedData)
        {
            var encodedBytes = encoded.HexToByteArray();
            var decoded = RLP.Decode(encodedBytes);

            if (expectedData == null)
            {
                Assert.Null(decoded.RLPData);
            }
            else
            {
                Assert.Equal(expectedData, decoded.RLPData);
            }
        }
    }
}
