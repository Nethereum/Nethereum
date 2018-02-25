using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Web3.Tests.Unit
{
 
    public class RLPTests
    {
        [Fact]
        public void ShouldEncodeEmptyString()
        {
            string test = "";
            byte[] testBytes = Encoding.UTF8.GetBytes(test);
            string expected = "80";
            byte[] encoderesult = RLP.RLP.EncodeElement(testBytes);
            Assert.Equal(expected, encoderesult.ToHex());

            var decodeResult = RLP.RLP.Decode(encoderesult)[0].RLPData;
            Assert.Equal(null, decodeResult);
        }

        [Fact]
        public void ShouldEncodeShortString()
        {
            string test = "dog";
            string expected = "83646f67";
            AssertStringEncoding(test, expected);
        }

        [Fact]
        public void ShouldEncodeSingleCharacter()
        {
            string test = "d";
            string expected = "64";
            AssertStringEncoding(test, expected);

        }

        [Fact]
        public void ShouldEncodeLongString()
        {
            string test = "Lorem ipsum dolor sit amet, consectetur adipisicing elit"; // length = 56
            string expected = "b8384c6f72656d20697073756d20646f6c6f722073697420616d65742c20636f6e7365637465747572206164697069736963696e6720656c6974";
            AssertStringEncoding(test, expected);
        }

        private static void AssertStringEncoding(string test, string expected)
        {
            byte[] testBytes = test.ToBytesForRLPEncoding();
            byte[] encoderesult = RLP.RLP.EncodeElement(testBytes);
            Assert.Equal(expected, encoderesult.ToHex());

            var decodeResult = RLP.RLP.Decode(encoderesult)[0].RLPData;
            Assert.Equal(test, decodeResult.ToStringFromRLPDecoded());
        }

        [Fact]
        public void ShouldEncodeZeroAs80()
        {
            int test = 0;
            string expected = "80";
            AssertIntEncoding(test, expected);
        }

        [Fact]
        public void ShouldEncodeSmallInteger()
        {
            int test = 15;
            string expected = "0f";
            AssertIntEncoding(test, expected);
        }

        [Fact]
        public void ShouldEncodeMediumInteger()
        {
            int test = 1000;
            string expected = "8203e8";

            AssertIntEncoding(test, expected);
            test = 1024;
            expected = "820400";
            AssertIntEncoding(test, expected);
        }

        private static void AssertIntEncoding(int test, string expected)
        {
            byte[] testBytes = test.ToBytesForRLPEncoding();
            byte[] encoderesult = RLP.RLP.EncodeElement(testBytes);
            Assert.Equal(expected, encoderesult.ToHex());

            var decodeResult = RLP.RLP.Decode(encoderesult)[0].RLPData;
            Assert.Equal(test, decodeResult.ToBigIntegerFromRLPDecoded());
        }

        [Fact]
        public void ShouldEncodeBigInteger()
        {
            BigInteger test = "100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f".HexToByteArray().ToBigIntegerFromRLPDecoded();
            string expected = "a0100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
            byte[] testBytes = test.ToBytesForRLPEncoding();
            byte[] encoderesult = RLP.RLP.EncodeElement(testBytes);
            Assert.Equal(expected, encoderesult.ToHex());

            var decodeResult = RLP.RLP.Decode(encoderesult)[0].RLPData;
            Assert.Equal(test, decodeResult.ToBigIntegerFromRLPDecoded());
        }

        [Fact]
        public void ShouldEncodeEmptyList()
        {
            byte[][] test = new byte[0][];
            string expected = "c0";
            byte[] encoderesult = RLP.RLP.EncodeList(test);
            Assert.Equal(expected, encoderesult.ToHex());

            var decodeResult = RLP.RLP.Decode(encoderesult)[0] as RLPCollection;
            Assert.True(decodeResult.Count == 0);
        }

        [Fact]
        public void ShouldEncodeShortStringList()
        {
            string[] test = new string[] { "cat", "dog" };
            string expected = "c88363617483646f67";

            AssertStringCollection(test, expected);
            test = new string[] { "dog", "god", "cat" };
            expected = "cc83646f6783676f6483636174";
            AssertStringCollection(test, expected);
        }

        [Fact]
        public void ShouldEncodeLongStringList()
        {
            string element1 = "cat";
            string element2 = "Lorem ipsum dolor sit amet, consectetur adipisicing elit";
            string[] test = new string[] { element1, element2 };
            string expected = "f83e83636174b8384c6f72656d20697073756d20646f6c6f722073697420616d65742c20636f6e7365637465747572206164697069736963696e6720656c6974";
            AssertStringCollection(test, expected);
        }

        private static void AssertStringCollection(string[] test, string expected)
        {
            byte[] encoderesult = RLP.RLP.EncodeList(EncodeElementsBytes(test.ToBytesForRLPEncoding()));
            Assert.Equal(expected, encoderesult.ToHex());

            var decodeResult = RLP.RLP.Decode(encoderesult)[0] as RLPCollection;
            for (int i = 0; i < test.Length; i++)
            {
                Assert.Equal(test[i], decodeResult[i].RLPData.ToStringFromRLPDecoded());
            }
        }

        private static byte[][] EncodeElementsBytes(params byte[][] bytes)
        {
            var encodeElements = new List<byte[]>();
            foreach (var byteElement in bytes)
            {
                encodeElements.Add(RLP.RLP.EncodeElement(byteElement));
            }
            return encodeElements.ToArray();
        }
    }
}