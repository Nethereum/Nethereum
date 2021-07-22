using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class FunctionEncodingTests
    {
        public ParameterOutput CreateParamO(string type, string name, Type decodedType)
        {
            return new() {Parameter = CreateParam(type, name, decodedType)};
        }

        public Parameter CreateParam(string type, string name, Type decodedType = null)
        {
            return new(type, name) {DecodedType = decodedType};
        }

        [Fact]
        public virtual void ShouldDecodeMultipleArrays()
        {
            var functionCallDecoder = new FunctionCallDecoder();

            var outputParameters = new[]
            {
                CreateParamO("uint[]", "a", typeof(List<int>)),
                CreateParamO("uint[]", "b", typeof(List<int>))
            };

            var result = functionCallDecoder.DecodeOutput(
                "0x" +
                "000000000000000000000000000000000000000000000000000000000000004000000000000000000000000000000000000000000000000000000000000000c000000000000000000000000000000000000000000000000000000000000000030000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000003000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000002",
                outputParameters);
            Assert.True(result.Count == 2);
            var output1 = (List<int>) result[0].Result;
            var output2 = (List<int>) result[1].Result;

            Assert.Equal(3, output1.Count);
            Assert.Equal(3, output2.Count);

            Assert.Equal(0, output1[0]);
            Assert.Equal(1, output1[1]);
            Assert.Equal(2, output1[2]);

            Assert.Equal(0, output2[0]);
            Assert.Equal(1, output2[1]);
            Assert.Equal(2, output2[2]);
        }

        [Fact]
        public virtual void ShouldDecodeMultipleTypesIncludingDynamicStringAndIntArray()
        {
            var functionCallDecoder = new FunctionCallDecoder();

            var outputParameters = new[]
            {
                CreateParamO("string", "a", typeof(string)),
                CreateParamO("uint[20]", "b", typeof(List<uint>)),
                CreateParamO("string", "c", typeof(string))
            };

            var array = new uint[20];
            for (uint i = 0; i < 20; i++)
                array[i] = i + 234567;

            var result = functionCallDecoder.DecodeOutput(
                "0x" +
                "00000000000000000000000000000000000000000000000000000000000002c0000000000000000000000000000000000000000000000000000000000003944700000000000000000000000000000000000000000000000000000000000394480000000000000000000000000000000000000000000000000000000000039449000000000000000000000000000000000000000000000000000000000003944a000000000000000000000000000000000000000000000000000000000003944b000000000000000000000000000000000000000000000000000000000003944c000000000000000000000000000000000000000000000000000000000003944d000000000000000000000000000000000000000000000000000000000003944e000000000000000000000000000000000000000000000000000000000003944f0000000000000000000000000000000000000000000000000000000000039450000000000000000000000000000000000000000000000000000000000003945100000000000000000000000000000000000000000000000000000000000394520000000000000000000000000000000000000000000000000000000000039453000000000000000000000000000000000000000000000000000000000003945400000000000000000000000000000000000000000000000000000000000394550000000000000000000000000000000000000000000000000000000000039456000000000000000000000000000000000000000000000000000000000003945700000000000000000000000000000000000000000000000000000000000394580000000000000000000000000000000000000000000000000000000000039459000000000000000000000000000000000000000000000000000000000003945a0000000000000000000000000000000000000000000000000000000000000300000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000",
                outputParameters);

            Assert.Equal("hello", result.First(x => x.Parameter.Name == "a").Result);
            Assert.Equal("world", result.First(x => x.Parameter.Name == "c").Result);
        }

        [Fact]
        public virtual void ShouldEncodeAddress()
        {
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "c6888fa1";
            var inputsParameters = new[] {CreateParam("address", "a")};
            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters,
                "1234567890abcdef1234567890abcdef12345678");
            Assert.Equal("0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef12345678", result);
        }

        [Fact]
        public virtual void ShouldEncodeBool()
        {
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "c6888fa1";
            var inputsParameters = new[] {CreateParam("bool", "a")};
            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters, true);
            Assert.Equal("0xc6888fa10000000000000000000000000000000000000000000000000000000000000001", result);
        }

        [Fact]
        public virtual void ShouldEncodeInt()
        {
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "c6888fa1";
            var inputsParameters = new[] {CreateParam("int", "a")};
            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters, 69);
            Assert.Equal("0xc6888fa10000000000000000000000000000000000000000000000000000000000000045", result);
        }

        [Fact]
        public virtual void ShouldEncodeMultipleTypes()
        {
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "c6888fa1";
            var inputsParameters = new[]
                {CreateParam("address", "a"), CreateParam("int", "b"), CreateParam("int", "c")};

            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters,
                "1234567890abcdef1234567890abcdef12345678", 69, 69);
            Assert.Equal(
                "0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef1234567800000000000000000000000000000000000000000000000000000000000000450000000000000000000000000000000000000000000000000000000000000045",
                result);
        }

        [Fact]
        public virtual void ShouldEncodeMultipleTypesIncludingDynamicStringAndIntArray()
        {
            var paramsEncoded =
                "00000000000000000000000000000000000000000000000000000000000002c0000000000000000000000000000000000000000000000000000000000003944700000000000000000000000000000000000000000000000000000000000394480000000000000000000000000000000000000000000000000000000000039449000000000000000000000000000000000000000000000000000000000003944a000000000000000000000000000000000000000000000000000000000003944b000000000000000000000000000000000000000000000000000000000003944c000000000000000000000000000000000000000000000000000000000003944d000000000000000000000000000000000000000000000000000000000003944e000000000000000000000000000000000000000000000000000000000003944f0000000000000000000000000000000000000000000000000000000000039450000000000000000000000000000000000000000000000000000000000003945100000000000000000000000000000000000000000000000000000000000394520000000000000000000000000000000000000000000000000000000000039453000000000000000000000000000000000000000000000000000000000003945400000000000000000000000000000000000000000000000000000000000394550000000000000000000000000000000000000000000000000000000000039456000000000000000000000000000000000000000000000000000000000003945700000000000000000000000000000000000000000000000000000000000394580000000000000000000000000000000000000000000000000000000000039459000000000000000000000000000000000000000000000000000000000003945a0000000000000000000000000000000000000000000000000000000000000300000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";


            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "c6888fa1";
            var inputsParameters = new[]
                {CreateParam("string", "a"), CreateParam("uint[20]", "b"), CreateParam("string", "c")};

            var array = new uint[20];
            for (uint i = 0; i < 20; i++)
                array[i] = i + 234567;

            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters, "hello", array, "world");

            Assert.Equal("0x" + sha3Signature + paramsEncoded, result);
        }


        [Fact]
        public virtual void ShouldEncodeMultipleTypesIncludingDynamicString()
        {
            var paramsEncoded =
                "0000000000000000000000000000000000000000000000000000000000000060000000000000000000000000000000000000000000000000000000000000004500000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "c6888fa1";
            var inputsParameters = new[]
                {CreateParam("string", "a"), CreateParam("int", "b"), CreateParam("string", "c")};

            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters, "hello", 69, "world");
            Assert.Equal("0x" + sha3Signature + paramsEncoded, result);
        }

        [Fact]
        public virtual void WhenAnAddressParameterValueIsNull_ShouldProvideAHelpfulError()
        {
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "c6888fa1";
            var inputsParameters = new[] {CreateParam("address", "_address1")};
            var parameterValues = new object[] {null};

            var ex = Assert.Throws<AbiEncodingException>(() =>
                functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters, parameterValues));

            const string ExpectedError =
                "An error occurred encoding abi value. Order: '1', Type: 'address', Value: 'null'.  Ensure the value is valid for the abi type.";

            Assert.Equal(ExpectedError, ex.Message);
        }
    }
}