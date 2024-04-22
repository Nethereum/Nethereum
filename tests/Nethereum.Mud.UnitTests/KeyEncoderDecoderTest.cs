using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Numerics;


namespace Nethereum.Mud.UnitTests
{
  


    public class KeyEncoderDecoderTest
    {
        public class TestKey
        {
            [Parameter("uint256", "field1", 1)]
            public BigInteger Field1 { get; set; }

            [Parameter("int32", "field2", 2)]
            public int Field2 { get; set; }

            [Parameter("bytes16", "field3", 3)]
            public byte[] Field3 { get; set; }

            [Parameter("address", "field4", 4)]
            public string Field4 { get; set; }

            [Parameter("bool", "field5", 5)]
            public bool Field5 { get; set; }

            [Parameter("int8", "field6", 6)]
            public sbyte Field6 { get; set; }
        }


        [Fact]
        public void ShouldEncodeComplexKeyTuple()
        {
            var fields = new List<FieldValue>()
            {
                new FieldValue("uint256", 42, true, "field1", 1),
                new FieldValue("int32", -42, true, "field2", 2),
                new FieldValue("bytes16", "0x12340000000000000000000000000000".HexToByteArray(), true, "field3", 3),
                new FieldValue("address", "0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF", true, "field4", 4),
                new FieldValue("bool", true, true, "field4", 5),
                new FieldValue("int8", 3, true, "field4", 6),
            };

            var keyEncoded = KeyEncoderDecoder.EncodeKey(fields);
            Assert.Equal("000000000000000000000000000000000000000000000000000000000000002a",keyEncoded[0].ToHex());
            Assert.Equal("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffd6", keyEncoded[1].ToHex());
            Assert.Equal("1234000000000000000000000000000000000000000000000000000000000000", keyEncoded[2].ToHex());
            Assert.Equal("000000000000000000000000ffffffffffffffffffffffffffffffffffffffff", keyEncoded[3].ToHex());
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000001", keyEncoded[4].ToHex());
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000003", keyEncoded[5].ToHex());

           
        }

        [Fact]
        public void ShouldEncodeComplexKeyUsingClass()
        {
            var key = new TestKey
            {
                Field1 = 42,
                Field2 = -42,
                Field3 = "0x12340000000000000000000000000000".HexToByteArray(),
                Field4 = "0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF",
                Field5 = true,
                Field6 = 3
            };
            var keyEncoded = KeyEncoderDecoder.EncodeKey(key);
            Assert.Equal("000000000000000000000000000000000000000000000000000000000000002a", keyEncoded[0].ToHex());
            Assert.Equal("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffd6", keyEncoded[1].ToHex());
            Assert.Equal("1234000000000000000000000000000000000000000000000000000000000000", keyEncoded[2].ToHex());
            Assert.Equal("000000000000000000000000ffffffffffffffffffffffffffffffffffffffff", keyEncoded[3].ToHex());
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000001", keyEncoded[4].ToHex());
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000003", keyEncoded[5].ToHex());
        }

        [Fact]
        public void ShouldDecodeComplexKeyUsingClass()
        {
            var keyEncoded = new List<byte[]>
            {
                "000000000000000000000000000000000000000000000000000000000000002a".HexToByteArray(),
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffd6".HexToByteArray(),
                "1234000000000000000000000000000000000000000000000000000000000000".HexToByteArray(),
                "000000000000000000000000ffffffffffffffffffffffffffffffffffffffff".HexToByteArray(),
                "0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray(),
                "0000000000000000000000000000000000000000000000000000000000000003".HexToByteArray()
            };

            var result = KeyEncoderDecoder.DecodeKey<TestKey>(keyEncoded);
            Assert.Equal(42, result.Field1);
            Assert.Equal(-42, result.Field2);
            Assert.Equal("0x12340000000000000000000000000000", result.Field3.ToHex(true));
            Assert.Equal("0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF", result.Field4);
            Assert.True(result.Field5);
            Assert.Equal(3, result.Field6);

            result = KeyEncoderDecoder.DecodeKey<TestKey>(ByteUtil.Merge(keyEncoded.ToArray()));
            Assert.Equal(42, result.Field1);
            Assert.Equal(-42, result.Field2);
            Assert.Equal("0x12340000000000000000000000000000", result.Field3.ToHex(true));
            Assert.Equal("0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF", result.Field4);
            Assert.True(result.Field5);
            Assert.Equal(3, result.Field6);

        }

        [Fact]
        public void ShouldDecodeComplexKeyTuple()
        {
            var fields = new List<FieldInfo>()
            {
                new FieldInfo("uint256", true, "field1", 1),
                new FieldInfo("int32", true, "field2", 2),
                new FieldInfo("bytes16", true, "field3", 3),
                new FieldInfo("address", true, "field4", 4),
                new FieldInfo("bool", true, "field4", 5),
                new FieldInfo("int8", true, "field4", 6),
            };

            var keyEncoded = new List<byte[]>
            {
                "000000000000000000000000000000000000000000000000000000000000002a".HexToByteArray(),
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffd6".HexToByteArray(),
                "1234000000000000000000000000000000000000000000000000000000000000".HexToByteArray(),
                "000000000000000000000000ffffffffffffffffffffffffffffffffffffffff".HexToByteArray(),
                "0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray(),
                "0000000000000000000000000000000000000000000000000000000000000003".HexToByteArray()
            };

            var result = KeyEncoderDecoder.DecodeKey(ByteUtil.Merge(keyEncoded.ToArray()), fields);
            Assert.Equal(42, (BigInteger)result[0]);
            Assert.Equal(-42, (BigInteger)result[1]);
            Assert.Equal("0x12340000000000000000000000000000", ((byte[])result[2]).ToHex(true));
            Assert.Equal("0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF", result[3]);
            Assert.Equal(true, result[4]);
            Assert.Equal(3, (BigInteger)result[5]);
        }

        [Fact]
        public void ShouldDecodeComplexKeyTupleList()
        {
            var fields = new List<FieldInfo>()
            {
                new FieldInfo("uint256", true, "field1", 1),
                new FieldInfo("int32", true, "field2", 2),
                new FieldInfo("bytes16", true, "field3", 3),
                new FieldInfo("address", true, "field4", 4),
                new FieldInfo("bool", true, "field4", 5),
                new FieldInfo("int8", true, "field4", 6),
            };

            var keyEncoded = new List<byte[]>
            {
                "000000000000000000000000000000000000000000000000000000000000002a".HexToByteArray(),
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffd6".HexToByteArray(),
                "1234000000000000000000000000000000000000000000000000000000000000".HexToByteArray(),
                "000000000000000000000000ffffffffffffffffffffffffffffffffffffffff".HexToByteArray(),
                "0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray(),
                "0000000000000000000000000000000000000000000000000000000000000003".HexToByteArray()
            };

            var result = KeyEncoderDecoder.DecodeKey(keyEncoded, fields);
            Assert.Equal(42, (BigInteger)result[0]);
            Assert.Equal(-42, (BigInteger)result[1]);
            Assert.Equal("0x12340000000000000000000000000000", ((byte[])result[2]).ToHex(true));
            Assert.Equal("0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF", result[3]);
            Assert.Equal(true, result[4]);
            Assert.Equal(3, (BigInteger)result[5]);
        }


    }
}