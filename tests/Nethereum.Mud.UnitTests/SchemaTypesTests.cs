using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud.EncodingDecoding;


namespace Nethereum.Mud.UnitTests
{

    public class SchemaTypesTests
    {
        [Fact]
        public void ShouldEncodeSchema()
        {
            var result = SchemaEncoder.EncodeTypesToHex("uint256", "uint256[]", "uint256[]");
            var result1 = SchemaEncoder.EncodeTypesToHex("uint8", "uint256[]", "uint256[]");
            var result2 = SchemaEncoder.EncodeTypesToHex("bool", "uint256[]", "uint256[]");
        }

        public class TableValues
        {
            [Parameter("uint32", "field1", 1)]
            public uint Field1 { get; set; }

            [Parameter("uint128", "field2", 2)]
            public BigInteger Field2 { get; set; }

            [Parameter("uint32[]", "field3", 3)]
            public List<uint> Field3 { get; set; }

            [Parameter("string", "field4", 4)]
            public string Field4 { get; set; }
        }

        [Fact]
        public void ShouldDecodeValuesToType()
        {
            var result = ValueEncoderDecoder.DecodeValues<TableValues>("0x0000000100000000000000000000000000000002000000000000000000000000000000000000000b0000000008000000000000130000000300000004736f6d6520737472696e67");
            Assert.Equal((uint)1, result.Field1);
            Assert.Equal(2, result.Field2);
            Assert.Equal((uint)3, result.Field3[0]);
            Assert.Equal((uint)4, result.Field3[1]);
            Assert.Equal("some string", result.Field4);
        }

        [Fact]
        public void ShouldEncodeValuesFromType()
        {
            var fieldValues = new TableValues()
            {
                Field1 = 1,
                Field2 = 2,
                Field3 = new List<uint> { 3, 4 },
                Field4 = "some string"
            };
            var result = ValueEncoderDecoder.EncodeValuesAsyByteArray(fieldValues);
            Assert.Equal("0x0000000100000000000000000000000000000002000000000000000000000000000000000000000b0000000008000000000000130000000300000004736f6d6520737472696e67",
                result.ToHex(true));
        }

        [Fact]
        public void ShouldDecodeValues()
        {
            var fields = new List<FieldInfo>()
            {
                new FieldInfo("uint32", false, "field1", 1),
                new FieldInfo("uint128", false, "field2", 2),
                new FieldInfo("uint32[]", false, "field3", 3),
                new FieldInfo("string", false, "field4", 4),
            };
            var data = "0x0000000100000000000000000000000000000002000000000000000000000000000000000000000b0000000008000000000000130000000300000004736f6d6520737472696e67".HexToByteArray();
            var result = ValueEncoderDecoder.DecodeValues(data, fields);

            Assert.Equal(1, (BigInteger)result[0]);
            Assert.Equal(2, (BigInteger)result[1]);
            Assert.Equal(3, ((List<BigInteger>)result[2])[0]);
            Assert.Equal(4, ((List<BigInteger>)result[2])[1]);
            Assert.Equal("some string", result[3]);
            
        }

        [Fact]
        public void ShouldEncodeValues()
        {
            var fieldValues = new List<FieldValue>() {
                new FieldValue("uint32", 1, "field1", 1),
                new FieldValue("uint128", 2, "field2", 2),
                new FieldValue("uint32[]", new List<BigInteger>{ 3, 4}, "field3", 3),
                new FieldValue("string", "some string" , "field4", 4),

            };
            var result = ValueEncoderDecoder.EncodeValuesAsyByteArray(fieldValues);
            Assert.Equal("0x0000000100000000000000000000000000000002000000000000000000000000000000000000000b0000000008000000000000130000000300000004736f6d6520737472696e67",
                result.ToHex(true));
        }


        [Fact]
        public void ShouldDecodeOutOfBoundsArray() //This test is not passing need to understand the scenario
        {
            var fields = new List<FieldInfo>()
            {
                new FieldInfo("uint32[]", false, "field1", 1),
            };
            var data = "0x0000000000000000000000000000000000000000000000000400000000000004".HexToByteArray();
            var result = ValueEncoderDecoder.DecodeValues(data, fields);

            Assert.Equal(0, ((List<BigInteger>)result[0])[0]);
        }

        [Fact]
        public void ShouldDecodeEmptyValues()
        {
            var fields = new List<FieldInfo>()
            {
                new FieldInfo("string", false, "field1", 1),
                new FieldInfo("string", false, "field2", 2),

            };
            var data = "0x0000000000000000000000000000000000000000000000000000000000000000".HexToByteArray();
            var result = ValueEncoderDecoder.DecodeValues(data, fields);

            Assert.Equal("", result[0]);
            Assert.Equal("", result[1]);

        }

      

        [Fact]
        public void ShouldDecodeEncodedLengths()
        {
            var result = EncodedLengthsEncoderDecoder.Decode("0x0000000000000000000000000000400000000020000000002000000000000080".HexToByteArray());

            
        }
      
    }
}