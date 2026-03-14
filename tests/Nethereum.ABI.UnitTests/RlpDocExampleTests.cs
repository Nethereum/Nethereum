using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using Xunit;
using RlpEncoder = Nethereum.RLP.RLP;

namespace Nethereum.ABI.UnitTests
{
    public class RlpDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Encode strings")]
        public void ShouldEncodeStrings()
        {
            string empty = "";
            byte[] emptyBytes = empty.ToBytesForRLPEncoding();
            byte[] encodedEmpty = RlpEncoder.EncodeElement(emptyBytes);
            Assert.Equal("80", encodedEmpty.ToHex());

            string single = "d";
            byte[] singleBytes = single.ToBytesForRLPEncoding();
            byte[] encodedSingle = RlpEncoder.EncodeElement(singleBytes);
            Assert.Equal("64", encodedSingle.ToHex());

            string dog = "dog";
            byte[] dogBytes = dog.ToBytesForRLPEncoding();
            byte[] encodedDog = RlpEncoder.EncodeElement(dogBytes);
            Assert.Equal("83646f67", encodedDog.ToHex());

            IRLPElement decoded = RlpEncoder.Decode(encodedDog);
            string decodedStr = decoded.RLPData.ToStringFromRLPDecoded();
            Assert.Equal("dog", decodedStr);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Encode integers")]
        public void ShouldEncodeIntegers()
        {
            int zero = 0;
            byte[] zeroBytes = zero.ToBytesForRLPEncoding();
            byte[] encodedZero = RlpEncoder.EncodeElement(zeroBytes);
            Assert.Equal("80", encodedZero.ToHex());

            int small = 15;
            byte[] smallBytes = small.ToBytesForRLPEncoding();
            byte[] encodedSmall = RlpEncoder.EncodeElement(smallBytes);
            Assert.Equal("0f", encodedSmall.ToHex());

            int medium = 1000;
            byte[] mediumBytes = medium.ToBytesForRLPEncoding();
            byte[] encodedMedium = RlpEncoder.EncodeElement(mediumBytes);
            Assert.Equal("8203e8", encodedMedium.ToHex());

            IRLPElement decoded = RlpEncoder.Decode(encodedMedium);
            int decodedInt = decoded.RLPData.ToIntFromRLPDecoded();
            Assert.Equal(1000, decodedInt);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Encode BigInteger")]
        public void ShouldEncodeBigInteger()
        {
            byte[] hexBytes = "100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
                .HexToByteArray();
            BigInteger bigInt = hexBytes.ToBigIntegerFromRLPDecoded();

            byte[] bigIntBytes = bigInt.ToBytesForRLPEncoding();
            byte[] encoded = RlpEncoder.EncodeElement(bigIntBytes);
            Assert.Equal(
                "a0100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
                encoded.ToHex()
            );

            IRLPElement decoded = RlpEncoder.Decode(encoded);
            BigInteger decodedBigInt = decoded.RLPData.ToBigIntegerFromRLPDecoded();
            Assert.Equal(bigInt, decodedBigInt);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Encode lists")]
        public void ShouldEncodeLists()
        {
            byte[][] emptyList = new byte[0][];
            byte[] encodedEmpty = RlpEncoder.EncodeList(emptyList);
            Assert.Equal("c0", encodedEmpty.ToHex());

            RLPCollection decodedEmpty = RlpEncoder.Decode(encodedEmpty) as RLPCollection;
            Assert.Equal(0, decodedEmpty.Count);

            string[] strings = { "cat", "dog" };
            byte[][] stringBytes = strings.ToBytesForRLPEncoding();

            byte[][] encodedElements = new byte[stringBytes.Length][];
            for (int i = 0; i < stringBytes.Length; i++)
            {
                encodedElements[i] = RlpEncoder.EncodeElement(stringBytes[i]);
            }

            byte[] encodedList = RlpEncoder.EncodeList(encodedElements);
            Assert.Equal("c88363617483646f67", encodedList.ToHex());

            RLPCollection decodedList = RlpEncoder.Decode(encodedList) as RLPCollection;
            Assert.Equal("cat", decodedList[0].RLPData.ToStringFromRLPDecoded());
            Assert.Equal("dog", decodedList[1].RLPData.ToStringFromRLPDecoded());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Encode long strings")]
        public void ShouldEncodeLongStrings()
        {
            string longString = "Lorem ipsum dolor sit amet, consectetur adipisicing elit";
            byte[] longBytes = longString.ToBytesForRLPEncoding();
            byte[] encoded = RlpEncoder.EncodeElement(longBytes);

            Assert.Equal(
                "b8384c6f72656d20697073756d20646f6c6f722073697420616d65742c20636f6e7365637465747572206164697069736963696e6720656c6974",
                encoded.ToHex()
            );

            IRLPElement decoded = RlpEncoder.Decode(encoded);
            string decodedStr = decoded.RLPData.ToStringFromRLPDecoded();
            Assert.Equal(longString, decodedStr);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Mixed list with long string")]
        public void ShouldEncodeMixedListWithLongString()
        {
            string shortStr = "cat";
            string longStr = "Lorem ipsum dolor sit amet, consectetur adipisicing elit";
            string[] mixed = { shortStr, longStr };

            byte[][] mixedBytes = mixed.ToBytesForRLPEncoding();

            byte[][] encodedElements = new byte[mixedBytes.Length][];
            for (int i = 0; i < mixedBytes.Length; i++)
            {
                encodedElements[i] = RlpEncoder.EncodeElement(mixedBytes[i]);
            }

            byte[] encodedList = RlpEncoder.EncodeList(encodedElements);
            Assert.Equal(
                "f83e83636174b8384c6f72656d20697073756d20646f6c6f722073697420616d65742c20636f6e7365637465747572206164697069736963696e6720656c6974",
                encodedList.ToHex()
            );

            RLPCollection decoded = RlpEncoder.Decode(encodedList) as RLPCollection;
            Assert.Equal("cat", decoded[0].RLPData.ToStringFromRLPDecoded());
            Assert.Equal(longStr, decoded[1].RLPData.ToStringFromRLPDecoded());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Multiple string list")]
        public void ShouldEncodeMultipleStringList()
        {
            string[] animals = { "dog", "god", "cat" };
            byte[][] animalBytes = animals.ToBytesForRLPEncoding();

            byte[][] encodedElements = new byte[animalBytes.Length][];
            for (int i = 0; i < animalBytes.Length; i++)
            {
                encodedElements[i] = RlpEncoder.EncodeElement(animalBytes[i]);
            }

            byte[] encodedList = RlpEncoder.EncodeList(encodedElements);
            Assert.Equal("cc83646f6783676f6483636174", encodedList.ToHex());

            RLPCollection decoded = RlpEncoder.Decode(encodedList) as RLPCollection;
            for (int i = 0; i < animals.Length; i++)
            {
                Assert.Equal(animals[i], decoded[i].RLPData.ToStringFromRLPDecoded());
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "rlp-encoding", "Raw bytes")]
        public void ShouldEncodeRawBytes()
        {
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            byte[] encoded = RlpEncoder.EncodeElement(data);

            IRLPElement decoded = RlpEncoder.Decode(encoded);
            byte[] decodedBytes = decoded.RLPData;

            Assert.Equal(data, decodedBytes);

            byte[] empty = new byte[0];
            byte[] encodedEmpty = RlpEncoder.EncodeElement(empty);
            Assert.Equal("80", encodedEmpty.ToHex());

            IRLPElement decodedEmpty = RlpEncoder.Decode(encodedEmpty);
            Assert.Null(decodedEmpty.RLPData);
        }
    }
}
