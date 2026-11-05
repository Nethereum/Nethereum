using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S10
{
    public class Eip712EncoderS10Tests
    {
        private const string Contract = "0xcccccccccccccccccccccccccccccccccccccccc";

        private static byte[] Keccak(params byte[][] parts)
        {
            return Sha3Keccack.Current.CalculateHash(parts.SelectMany(x => x).ToArray());
        }

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

        private static byte[] Word(BigInteger value)
        {
            var raw = value.ToByteArray().Reverse().SkipWhile(x => x == 0).ToArray();
            var word = new byte[32];
            Array.Copy(raw, 0, word, 32 - raw.Length, raw.Length);
            return word;
        }

        private static string Json(string messageTypes, string message, string extraTypes = "", string domain = "{\"name\":\"d\"}", string domainType = "[{\"name\":\"name\",\"type\":\"string\"}]")
        {
            return "{\"types\":{\"EIP712Domain\":" + domainType + ",\"M\":" + messageTypes + extraTypes + "},\"primaryType\":\"M\",\"domain\":" + domain + ",\"message\":" + message + "}";
        }

        private static string SingleField(string type, string valueJson)
        {
            return Json("[{\"name\":\"x\",\"type\":\"" + type + "\"}]", "{\"x\":" + valueJson + "}");
        }

        private static byte[] Encode(string json) => Eip712TypedDataEncoder.Current.EncodeTypedData(json);

        private static byte[] ExpectedFinal(byte[] domainHash, byte[] messageHash)
        {
            return Keccak(new byte[] { 0x19, 0x01 }, domainHash, messageHash);
        }

        private static byte[] DomainStructForName(string name)
        {
            return Keccak(Keccak(Utf8("EIP712Domain(string name)")), Keccak(Utf8(name)));
        }

        [Fact]
        public void S10_E01_Given_AMessageKeyNotDeclaredInTheType_When_Encoded_Then_ItHashesIdenticallyToTheMessageWithoutIt()
        {
            var withExtra = Json("[{\"name\":\"x\",\"type\":\"uint256\"}]", "{\"x\":1,\"recipient\":\"0x0000000000000000000000000000000000000001\"}");
            var without = Json("[{\"name\":\"x\",\"type\":\"uint256\"}]", "{\"x\":1}");
            Assert.Equal(Encode(without).ToHex(), Encode(withExtra).ToHex());
        }

        [Fact]
        public void S10_E02_Given_ADomainKeyNotDeclaredInTheDomainType_When_Encoded_Then_ItHashesIdenticallyToTheDomainWithoutIt()
        {
            var withExtra = Json("[{\"name\":\"x\",\"type\":\"uint256\"}]", "{\"x\":1}", domain: "{\"name\":\"d\",\"chainId\":1}");
            var without = Json("[{\"name\":\"x\",\"type\":\"uint256\"}]", "{\"x\":1}");
            Assert.Equal(Encode(without).ToHex(), Encode(withExtra).ToHex());
        }

        [Fact]
        public void S10_E03_Given_AStructNameContainingIntAndDigits_When_Encoded_Then_ItIsEncodedAsAStructPerEip712()
        {
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}],\"Line\":[{\"name\":\"a\",\"type\":\"Point1\"}],\"Point1\":[{\"name\":\"x\",\"type\":\"uint256\"}]},\"primaryType\":\"Line\",\"domain\":{\"name\":\"d\"},\"message\":{\"a\":{\"x\":1}}}";
            var point = Keccak(Keccak(Utf8("Point1(uint256 x)")), Word(1));
            var expected = ExpectedFinal(DomainStructForName("d"), Keccak(Keccak(Utf8("Line(Point1 a)Point1(uint256 x)")), point));
            Assert.Equal(expected.ToHex(), Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex());
            Assert.Equal("fbe64bf808570b4f2ab09c1746915d12c5898daaa4c6f81eebef5bcd08a6ecb5", expected.ToHex());
        }

        [Fact]
        public void S10_E04_Given_AStructNameContainingBytesAndDigits_When_Encoded_Then_ItIsEncodedAsAStructPerEip712()
        {
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}],\"Line\":[{\"name\":\"a\",\"type\":\"Mybytes4\"}],\"Mybytes4\":[{\"name\":\"x\",\"type\":\"uint256\"}]},\"primaryType\":\"Line\",\"domain\":{\"name\":\"d\"},\"message\":{\"a\":{\"x\":1}}}";
            var inner = Keccak(Keccak(Utf8("Mybytes4(uint256 x)")), Word(1));
            var expected = ExpectedFinal(DomainStructForName("d"), Keccak(Keccak(Utf8("Line(Mybytes4 a)Mybytes4(uint256 x)")), inner));
            Assert.Equal(expected.ToHex(), Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex());
            Assert.Equal("eaa80b53c00220a719e443bfdfafe32c13e2b0237caeb928353d669d89f55660", expected.ToHex());
        }

        [Theory]
        [InlineData("bytes4", "\"0x1234\"")]
        [InlineData("bytes4", "\"0x1234567890\"")]
        [InlineData("bytes32", "\"0x01\"")]
        [InlineData("bytes1", "\"0x\"")]
        public void S10_E05_Given_ABytesNValueOfTheWrongLength_When_Encoded_Then_ItIsRefused(string type, string value)
        {
            Assert.ThrowsAny<Exception>(() => Encode(SingleField(type, value)));
        }

        [Theory]
        [InlineData("\"0x1234\"")]
        [InlineData("\"0x\"")]
        [InlineData("\"0x00000000000000000000000000000000000000010\"")]
        [InlineData("\"notanaddress\"")]
        [InlineData("5")]
        [InlineData("-1")]
        [InlineData("\"0xffffffffffffffffffffffffcccccccccccccccccccccccccccccccccccccccc\"")]
        [InlineData("null")]
        public void S10_E06_Given_AnAddressThatIsNotTwentyBytesOfHex_When_Encoded_Then_ItIsRefused(string value)
        {
            Assert.ThrowsAny<Exception>(() => Encode(SingleField("address", value)));
        }

        [Theory]
        [InlineData("\"false\"")]
        [InlineData("\"true\"")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("2")]
        [InlineData("\"\"")]
        [InlineData("null")]
        public void S10_E07_Given_ABoolThatIsNotAJsonBoolean_When_Encoded_Then_ItIsRefused(string value)
        {
            Assert.ThrowsAny<Exception>(() => Encode(SingleField("bool", value)));
        }

        [Theory]
        [InlineData("uint8", "256")]
        [InlineData("uint8", "\"256\"")]
        [InlineData("uint8", "-1")]
        [InlineData("uint8", "\"-1\"")]
        [InlineData("int8", "128")]
        [InlineData("int8", "-129")]
        [InlineData("uint256", "\"115792089237316195423570985008687907853269984665640564039457584007913129639936\"")]
        [InlineData("int256", "\"-57896044618658097711785492504343953926634992332820282019728792003956564819969\"")]
        [InlineData("uint256", "null")]
        [InlineData("uint256", "true")]
        public void S10_E08_Given_AnIntegerOutOfTheRangeOfItsType_When_Encoded_Then_ItIsRefused(string type, string value)
        {
            Assert.ThrowsAny<Exception>(() => Encode(SingleField(type, value)));
        }

        [Theory]
        [InlineData("1.5")]
        [InlineData("1e30")]
        [InlineData("\" 5\"")]
        [InlineData("\"5 \"")]
        [InlineData("\"+5\"")]
        [InlineData("\"1e3\"")]
        [InlineData("\"ff\"")]
        [InlineData("\"5-\"")]
        [InlineData("\"1,000\"")]
        [InlineData("\"0.0\"")]
        public void S10_E09_Given_AnIntegerNotWrittenAsADecimalOrHexIntegerLiteral_When_Encoded_Then_ItIsRefused(string value)
        {
            Assert.ThrowsAny<Exception>(() => Encode(SingleField("uint256", value)));
        }

        [Theory]
        [InlineData("uint7")]
        [InlineData("uint264")]
        [InlineData("int0")]
        [InlineData("bytes33")]
        [InlineData("bytes0")]
        public void S10_E10_Given_AnInvalidPrimitiveWidth_When_Encoded_Then_ItIsRefused(string type)
        {
            var value = type.StartsWith("bytes") ? "\"0x00\"" : "1";
            Assert.ThrowsAny<Exception>(() => Encode(SingleField(type, value)));
        }

        [Theory]
        [InlineData("\"0xabc\"")]
        [InlineData("\"zz\"")]
        [InlineData("\"abcd\"")]
        [InlineData("null")]
        public void S10_E11_Given_ABytesValueThatIsNotCanonical0xHex_When_Encoded_Then_ItIsRefused(string value)
        {
            Assert.ThrowsAny<Exception>(() => Encode(SingleField("bytes", value)));
        }

        [Theory]
        [InlineData("bytes4[]", "[\"0x12345678\",\"0x1234567890\"]")]
        [InlineData("bytes4[]", "[\"0x1234\"]")]
        [InlineData("address[]", "[\"0x1234\"]")]
        [InlineData("bool[]", "[\"false\"]")]
        [InlineData("bool[]", "[2]")]
        [InlineData("uint8[]", "[256]")]
        [InlineData("uint8[][]", "[[256]]")]
        public void S10_E12_Given_AnArrayElementThatViolatesItsElementType_When_Encoded_Then_ItIsRefused(string type, string value)
        {
            Assert.ThrowsAny<Exception>(() => Encode(SingleField(type, value)));
        }

        [Fact]
        public void S10_E14_Given_TypesWhoseDependencyGraphBranchesAtEveryLevel_When_Encoded_Then_ItCompletesInLinearTime()
        {
            const int levels = 18;
            var types = new StringBuilder();
            for (var i = 0; i < levels; i++)
            {
                types.Append(",\"T" + i + "\":[{\"name\":\"a\",\"type\":\"T" + (i + 1) + "[]\"},{\"name\":\"b\",\"type\":\"U" + (i + 1) + "[]\"}]");
                types.Append(",\"U" + i + "\":[{\"name\":\"a\",\"type\":\"T" + (i + 1) + "[]\"},{\"name\":\"b\",\"type\":\"U" + (i + 1) + "[]\"}]");
            }
            types.Append(",\"T" + levels + "\":[{\"name\":\"v\",\"type\":\"uint256\"}],\"U" + levels + "\":[{\"name\":\"v\",\"type\":\"uint256\"}]");
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}]" + types + "},\"primaryType\":\"T0\",\"domain\":{\"name\":\"d\"},\"message\":{\"a\":[],\"b\":[]}}";

            var stopwatch = Stopwatch.StartNew();
            var task = Task.Run(() => Encode(json));
            var finished = task.Wait(TimeSpan.FromSeconds(2));
            stopwatch.Stop();

            Assert.True(finished, "a " + json.Length + " byte typed-data document took longer than 2 s to encode (still running after " + stopwatch.ElapsedMilliseconds + " ms)");
        }

        public class MailBody
        {
            [Parameter("string", "contents", 1)]
            public string Contents { get; set; }

            [Parameter("uint256", "amount", 2)]
            public BigInteger Amount { get; set; }
        }

        private static TypedData<Domain> NewTypedData()
        {
            return new TypedData<Domain>
            {
                Domain = new Domain { Name = "d", Version = "1", ChainId = 1, VerifyingContract = Contract },
                PrimaryType = "MailBody",
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(MailBody))
            };
        }

        [Fact]
        public void S10_E15_Given_OneTypedDataInstanceSharedBetweenThreads_When_DifferentMessagesAreHashed_Then_EachHashBelongsToItsOwnMessage()
        {
            var typedData = NewTypedData();
            var messages = Enumerable.Range(0, 4).Select(i => new MailBody { Contents = "m" + i, Amount = i + 1 }).ToArray();
            var expected = messages.Select(m => Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(m, NewTypedData()).ToHex()).ToArray();

            var mismatches = 0;
            Parallel.For(0, messages.Length * 2000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, n =>
            {
                var index = n % messages.Length;
                var actual = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(messages[index], typedData).ToHex();
                if (actual != expected[index]) System.Threading.Interlocked.Increment(ref mismatches);
            });

            Assert.Equal(0, mismatches);
        }

        [Fact]
        public void S10_E16_Given_ADeeplyNestedArrayValue_When_Encoded_Then_ItIsRefusedWithoutExhaustingTheStack()
        {
            var depth = 3000;
            var type = "uint256" + string.Concat(Enumerable.Repeat("[]", depth));
            var value = new string('[', depth) + new string(']', depth);
            Assert.ThrowsAny<Exception>(() => Encode(SingleField(type, value)));
        }

        [Fact]
        public void S10_E17_Given_AnEmptyMemberNameOrType_When_Encoded_Then_ItIsRefused()
        {
            Assert.ThrowsAny<Exception>(() => Encode(Json("[{\"name\":\"\",\"type\":\"uint256\"}]", "{\"\":1}")));
            Assert.ThrowsAny<Exception>(() => Encode(Json("[{\"name\":\"x\",\"type\":\"\"}]", "{\"x\":1}")));
        }

        [Fact]
        public void S10_E18_Given_TwoDifferentAddressStrings_When_Encoded_Then_TheyNeverProduceTheSameHash()
        {
            var plain = "\"0xcccccccccccccccccccccccccccccccccccccccc\"";
            var prefixed = "\"0xffffffffffffffffffffffffcccccccccccccccccccccccccccccccccccccccc\"";
            var plainHash = Encode(SingleField("address", plain)).ToHex();
            string prefixedHash;
            try
            {
                prefixedHash = Encode(SingleField("address", prefixed)).ToHex();
            }
            catch (Exception)
            {
                return;
            }
            Assert.NotEqual(plainHash, prefixedHash);
        }

        [Fact]
        public void S10_E19_Given_TwoDifferentBytes32Strings_When_Encoded_Then_TheyNeverProduceTheSameHashAsTheFullWidthValue()
        {
            var padded = "\"0x0100000000000000000000000000000000000000000000000000000000000000\"";
            var short1 = "\"0x01\"";
            var paddedHash = Encode(SingleField("bytes32", padded)).ToHex();
            string shortHash;
            try
            {
                shortHash = Encode(SingleField("bytes32", short1)).ToHex();
            }
            catch (Exception)
            {
                return;
            }
            Assert.NotEqual(paddedHash, shortHash);
        }

        [Theory]
        [InlineData("5")]
        [InlineData("\"5\"")]
        [InlineData("\"0x5\"")]
        [InlineData("12345678901234567890123")]
        public void S10_E20_Given_ADomainChainIdWrittenAsANumberOrString_When_TheChainIdIsReadFromTheDomain_Then_ItIsReturned(string chainId)
        {
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"},{\"name\":\"chainId\",\"type\":\"uint256\"}],\"M\":[{\"name\":\"x\",\"type\":\"uint256\"}]},\"primaryType\":\"M\",\"domain\":{\"name\":\"d\",\"chainId\":" + chainId + "},\"message\":{\"x\":1}}";
            var raw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(json);
            var read = raw.GetChainIdFromDomain();
            Assert.NotNull(read);
            var expected = chainId.Trim('"');
            Assert.Equal(expected.StartsWith("0x") ? BigInteger.Parse("0" + expected.Substring(2), System.Globalization.NumberStyles.HexNumber) : BigInteger.Parse(expected), read.Value);
        }

        [Fact]
        public void S10_E21_Given_ADomainWithoutAChainIdOrVerifyingContract_When_Encoded_Then_TheOmittedFieldsAreNotSignedAsZero()
        {
            var typedData = new TypedData<Domain>
            {
                Domain = new Domain { Name = "d", Version = "1" },
                PrimaryType = "MailBody",
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(MailBody))
            };
            var withZeros = new TypedData<Domain>
            {
                Domain = new Domain { Name = "d", Version = "1", ChainId = 0, VerifyingContract = "0x0000000000000000000000000000000000000000" },
                PrimaryType = "MailBody",
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(MailBody))
            };
            var message = new MailBody { Contents = "m", Amount = 1 };
            string omitted;
            try
            {
                omitted = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(message, typedData).ToHex();
            }
            catch (Exception)
            {
                return;
            }
            var zeros = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(message, withZeros).ToHex();
            Assert.NotEqual(zeros, omitted);
        }

        [Struct("Item")]
        public class ItemStruct
        {
            [Parameter("uint256", "a", 1)]
            public BigInteger A { get; set; }
        }

        [Struct("Wrapper")]
        public class WrapperStruct
        {
            [Parameter("tuple[]", "items", 1, "Item[]")]
            public List<ItemStruct> Items { get; set; }

            [Parameter("uint256", "n", 2)]
            public BigInteger N { get; set; }
        }

        [Struct("Outer")]
        public class OuterStruct
        {
            [Parameter("tuple", "w", 1, "Wrapper")]
            public WrapperStruct W { get; set; }
        }

        [Fact]
        public void S10_E22_Given_AStructNestedInAStructThatHoldsAnArrayOfStructs_When_EncodedFromClassesAndFromJson_Then_TheHashesAreEqual()
        {
            var typedData = new TypedData<Domain>
            {
                Domain = new Domain { Name = "d", Version = "1", ChainId = 1, VerifyingContract = Contract },
                PrimaryType = "Outer",
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(OuterStruct), typeof(WrapperStruct), typeof(ItemStruct))
            };
            var message = new OuterStruct { W = new WrapperStruct { Items = new List<ItemStruct> { new ItemStruct { A = 7 }, new ItemStruct { A = 9 } }, N = 3 } };
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"},{\"name\":\"version\",\"type\":\"string\"},{\"name\":\"chainId\",\"type\":\"uint256\"},{\"name\":\"verifyingContract\",\"type\":\"address\"}],"
                + "\"Outer\":[{\"name\":\"w\",\"type\":\"Wrapper\"}],\"Wrapper\":[{\"name\":\"items\",\"type\":\"Item[]\"},{\"name\":\"n\",\"type\":\"uint256\"}],\"Item\":[{\"name\":\"a\",\"type\":\"uint256\"}]},"
                + "\"primaryType\":\"Outer\",\"domain\":{\"name\":\"d\",\"version\":\"1\",\"chainId\":1,\"verifyingContract\":\"" + Contract + "\"},"
                + "\"message\":{\"w\":{\"items\":[{\"a\":7},{\"a\":9}],\"n\":3}}}";
            var fromJson = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex();
            string fromClasses;
            try
            {
                fromClasses = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(message, typedData).ToHex();
            }
            catch (Exception ex)
            {
                fromClasses = ex.GetType().Name + ": " + ex.Message;
            }
            Func<BigInteger, byte[]> itemHash = a => Keccak(Keccak(Utf8("Item(uint256 a)")), Word(a));
            var itemsHash = Keccak(itemHash(7), itemHash(9));
            var wrapperHash = Keccak(Keccak(Utf8("Wrapper(Item[] items,uint256 n)Item(uint256 a)")), itemsHash, Word(3));
            var outerHash = Keccak(Keccak(Utf8("Outer(Wrapper w)Item(uint256 a)Wrapper(Item[] items,uint256 n)")), wrapperHash);
            var domainHash = Keccak(Keccak(Utf8("EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)")), Keccak(Utf8("d")), Keccak(Utf8("1")), Word(1), new byte[12].Concat(Enumerable.Repeat((byte)0xcc, 20)).ToArray());
            var expected = Keccak(new byte[] { 0x19, 0x01 }, domainHash, outerHash);
            Assert.Equal(expected.ToHex(), fromJson);
            Assert.Equal(expected.ToHex(), fromClasses);
        }
    }
}
