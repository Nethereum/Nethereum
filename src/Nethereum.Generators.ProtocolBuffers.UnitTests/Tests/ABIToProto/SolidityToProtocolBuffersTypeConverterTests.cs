using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;
using Xunit;

namespace Nethereum.Generators.ProtocolBuffers.UnitTests.Tests.ABIToProto
{
    public class SolidityToProtocolBuffersTypeConverterTests
    {
        [Fact]
        public void ConvertDirectlyMappedTypes()
        {
            var converter = new SolidityToProtoBufTypeConverter();
            Assert.Equal("string", converter.Convert("string"));
            Assert.Equal("bytes", converter.Convert("bytes"));
            Assert.Equal("string", converter.Convert("address"));
            Assert.Equal("bool", converter.Convert("bool"));
        }

        [Fact]
        public void WhereArrayTypeIsUnknownTheInputTypeIsReturned()
        {
            var converter = new SolidityToProtoBufTypeConverter();
            Assert.Equal("repeated ObjectDef", converter.Convert("ObjectDef[]"));
        }

        [Fact]
        public void WhereTypeIsUnknownTheInputTypeIsReturned()
        {
            var converter = new SolidityToProtoBufTypeConverter();
            Assert.Equal("ObjectDef", converter.Convert("ObjectDef"));
        }

        [Fact]
        public void ConvertsTypesWithSpecificSize()
        {
            var converter = new SolidityToProtoBufTypeConverter();
            Assert.Equal("int32", converter.Convert("int"));
            Assert.Equal("int32", converter.Convert("int32"));
            Assert.Equal("int64", converter.Convert("int64"));
            Assert.Equal("uint32", converter.Convert("uint"));
            Assert.Equal("uint32", converter.Convert("uint32"));
            Assert.Equal("uint64", converter.Convert("uint64"));
        }

        [Fact]
        public void IntsGreaterThan64AreConvertedToBytes()
        {
            var converter = new SolidityToProtoBufTypeConverter();
            Assert.Equal("bytes", converter.Convert("int256"));
            Assert.Equal("bytes", converter.Convert("uint256"));
        }

        [Fact]
        public void ConvertsSizedByteArrayToBytes()
        {
            var converter = new SolidityToProtoBufTypeConverter();
            Assert.Equal("bytes", converter.Convert("bytes16"));
            Assert.Equal("bytes", converter.Convert("bytes32"));
            Assert.Equal("bytes", converter.Convert("bytes64"));
        }

        [Fact]
        public void AddsRepeatedTagWhenAbiTypeIsAnArray()
        {
            var converter = new SolidityToProtoBufTypeConverter();
            Assert.Equal("repeated int32", converter.Convert("int[]"));
            Assert.Equal("repeated int32", converter.Convert("int32[]"));
            Assert.Equal("repeated int64", converter.Convert("int64[]"));

            Assert.Equal("repeated uint32", converter.Convert("uint[]"));
            Assert.Equal("repeated uint32", converter.Convert("uint32[]"));
            Assert.Equal("repeated uint64", converter.Convert("uint64[]"));
        }

    }
}
