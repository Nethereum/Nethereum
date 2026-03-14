using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class AbiEncodingDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Basic function encoding")]
        public void ShouldEncodeBasicFunctionCall()
        {
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "a9059cbb";
            var inputsParameters = new[]
            {
                new Parameter("address", "to") { DecodedType = typeof(string) },
                new Parameter("uint256", "value") { DecodedType = typeof(BigInteger) }
            };

            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters,
                "1234567890abcdef1234567890abcdef12345678", new BigInteger(1000));

            Assert.StartsWith("0xa9059cbb", result);
            Assert.Equal(2 + 8 + 64 + 64, result.Length);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Decode function output")]
        public void ShouldDecodeFunctionOutput()
        {
            var functionCallDecoder = new FunctionCallDecoder();

            var outputParameters = new[]
            {
                new ParameterOutput
                {
                    Parameter = new Parameter("uint256", "balance") { DecodedType = typeof(BigInteger) }
                }
            };

            var encodedOutput = "0x" +
                "0000000000000000000000000000000000000000000000000000000000000045";

            var result = functionCallDecoder.DecodeOutput(encodedOutput, outputParameters);

            Assert.Single(result);
            Assert.Equal(new BigInteger(69), (BigInteger)result[0].Result);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Encode multiple types")]
        public void ShouldEncodeMultipleMixedTypes()
        {
            var functionCallEncoder = new FunctionCallEncoder();
            var sha3Signature = "abcd1234";
            var inputsParameters = new[]
            {
                new Parameter("string", "name"),
                new Parameter("uint256", "amount"),
                new Parameter("bool", "active"),
                new Parameter("address", "recipient")
            };

            var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters,
                "hello", new BigInteger(100), true, "1234567890abcdef1234567890abcdef12345678");

            Assert.NotEmpty(result);
            Assert.StartsWith("0xabcd1234", result);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Parameter attribute encoding")]
        public void ShouldEncodeUsingParameterAttributes()
        {
            var input = new TransferFunction
            {
                To = "1234567890abcdef1234567890abcdef12345678",
                Amount = new BigInteger(5000)
            };

            var result = new FunctionCallEncoder().EncodeRequest(input, "a9059cbb");

            Assert.StartsWith("0xa9059cbb", result);
            Assert.Contains("0000000000000000000000001234567890abcdef1234567890abcdef12345678", result);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Event topic decoding")]
        public void ShouldDecodeTransferEventTopic()
        {
            var keccak = Sha3Keccack.Current;
            var eventSignature = "Transfer(address,address,uint256)";
            var expectedTopicHash = keccak.CalculateHash(eventSignature);

            Assert.Equal("ddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef", expectedTopicHash);

            var topics = new[]
            {
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef",
                "0x0000000000000000000000000000000000000000000000000000000000000000",
                "0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91",
                "0x0000000000000000000000000000000000000000000000400000402000000001"
            };

            var transferDto = new TransferEventDTO();
            new EventTopicDecoder().DecodeTopics(transferDto, topics, "0x");

            Assert.True("0x0000000000000000000000000000000000000000".IsTheSameAddress(transferDto.From));
            Assert.True("0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91".IsTheSameAddress(transferDto.To));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Contract ABI deserialization")]
        public void ShouldDeserializeContractAbi()
        {
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""},{""name"":""to"",""type"":""address""}],""name"":""other"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""from"",""type"":""address""},{""indexed"":true,""name"":""to"",""type"":""address""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""}]";

            var des = new ABIJsonDeserialiser();
            var contract = des.DeserialiseContract(abi);

            Assert.Equal(2, contract.Functions.Length);
            Assert.Single(contract.Events);
            Assert.Equal("multiply", contract.Functions[0].Name);
            Assert.Equal("Transfer", contract.Events[0].Name);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Individual type encoding")]
        public void ShouldEncodeIndividualTypesWithPadding()
        {
            var addressType = new AddressType();
            var addressEncoded = addressType.Encode("1234567890abcdef1234567890abcdef12345678");
            Assert.Equal(32, addressEncoded.Length);

            var intType = new IntType("uint256");
            var intEncoded = intType.Encode(new BigInteger(42));
            Assert.Equal(32, intEncoded.Length);

            var boolType = new BoolType();
            var boolEncoded = boolType.Encode(true);
            Assert.Equal(32, boolEncoded.Length);

            var bytes32Type = new Bytes32Type("bytes32");
            var bytes32Value = new byte[32];
            bytes32Value[0] = 0xAB;
            bytes32Value[1] = 0xCD;
            var bytes32Encoded = bytes32Type.Encode(bytes32Value);
            Assert.Equal(32, bytes32Encoded.Length);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Custom error decoding")]
        public void ShouldDecodeCustomError()
        {
            var error = new ErrorABI("InsufficientBalance");
            error.InputParameters = new[]
            {
                new Parameter("address", "account", 1),
                new Parameter("uint256", "balance", 2)
            };

            var functionCallEncoder = new FunctionCallEncoder();
            var errorSelector = error.Sha3Signature;

            var encodedData = "0x" + errorSelector +
                "000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91" +
                "0000000000000000000000000000000000000000000000000000000000000064";

            var decoder = new FunctionCallDecoder();
            var decoded = decoder.DecodeError(error, encodedData);

            Assert.Equal(2, decoded.Count);
            Assert.True("0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91"
                .IsTheSameAddress(decoded[0].Result.ToString()));
            Assert.Equal(new BigInteger(100), (BigInteger)decoded[1].Result);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "Function selector calculation")]
        public void ShouldCalculateFunctionSelector()
        {
            var keccak = Sha3Keccack.Current;

            var transferSignature = "transfer(address,uint256)";
            var fullHash = keccak.CalculateHash(transferSignature);
            var selector = fullHash.Substring(0, 8);
            Assert.Equal("a9059cbb", selector);

            var balanceOfSignature = "balanceOf(address)";
            var balanceOfHash = keccak.CalculateHash(balanceOfSignature);
            var balanceOfSelector = balanceOfHash.Substring(0, 8);
            Assert.Equal("70a08231", balanceOfSelector);

            var approveSignature = "approve(address,uint256)";
            var approveHash = keccak.CalculateHash(approveSignature);
            var approveSelector = approveHash.Substring(0, 8);
            Assert.Equal("095ea7b3", approveSelector);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-encoding", "ABIEncode decode methods", Order = 10)]
        public void ShouldDecodeEncodedValues()
        {
            var abiEncode = new ABIEncode();

            // Encode then decode a BigInteger
            var encoded = abiEncode.GetABIEncoded(new ABIValue("uint256", new BigInteger(42)));
            var decoded = abiEncode.DecodeEncodedBigInteger(encoded);
            Assert.Equal(new BigInteger(42), decoded);

            // Encode then decode an address
            var addressEncoded = abiEncode.GetABIEncoded(new ABIValue("address", "0x407D73d8a49eeb85D32Cf465507dd71d507100c1"));
            var addressDecoded = abiEncode.DecodeEncodedAddress(addressEncoded);
            Assert.True("0x407D73d8a49eeb85D32Cf465507dd71d507100c1".IsTheSameAddress(addressDecoded));

            // Encode then decode a boolean
            var boolEncoded = abiEncode.GetABIEncoded(new ABIValue("bool", true));
            var boolDecoded = abiEncode.DecodeEncodedBoolean(boolEncoded);
            Assert.True(boolDecoded);

            // Encode then decode a string
            var stringEncoded = abiEncode.GetABIEncoded(new ABIValue("string", "hello"));
            var stringDecoded = abiEncode.DecodeEncodedString(stringEncoded);
            Assert.Equal("hello", stringDecoded);
        }

        [Function("transfer")]
        private class TransferFunction
        {
            [Parameter("address", "to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "amount", 2)]
            public BigInteger Amount { get; set; }
        }

        [Event("Transfer")]
        private class TransferEventDTO
        {
            [Parameter("address", "_from", 1, true)]
            public string From { get; set; }

            [Parameter("address", "_to", 2, true)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 3, true)]
            public BigInteger Value { get; set; }
        }
    }
}
