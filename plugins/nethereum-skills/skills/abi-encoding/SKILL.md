---
name: abi-encoding
description: Encode and decode Ethereum ABI data with Nethereum. Use when the user needs to encode function calls, decode contract output, work with event topics, parse ABI JSON, handle custom errors, or calculate function selectors.
user-invocable: true
---

# ABI Encoding with Nethereum

NuGet: `Nethereum.ABI`

## Function selector calculation

```csharp
var keccak = Sha3Keccack.Current;

var selector = keccak.CalculateHash("transfer(address,uint256)").Substring(0, 8);
// "a9059cbb"

var balanceOf = keccak.CalculateHash("balanceOf(address)").Substring(0, 8);
// "70a08231"

var approve = keccak.CalculateHash("approve(address,uint256)").Substring(0, 8);
// "095ea7b3"
```

Source: `AbiEncodingDocExampleTests.ShouldCalculateFunctionSelector`

## Function encoding with Parameter array

```csharp
var functionCallEncoder = new FunctionCallEncoder();
var sha3Signature = "a9059cbb";
var inputsParameters = new[]
{
    new Parameter("address", "to") { DecodedType = typeof(string) },
    new Parameter("uint256", "value") { DecodedType = typeof(BigInteger) }
};

var result = functionCallEncoder.EncodeRequest(sha3Signature, inputsParameters,
    "1234567890abcdef1234567890abcdef12345678", new BigInteger(1000));
```

Source: `AbiEncodingDocExampleTests.ShouldEncodeBasicFunctionCall`

## Parameter attribute encoding

```csharp
[Function("transfer")]
public class TransferFunction
{
    [Parameter("address", "to", 1)]
    public string To { get; set; }

    [Parameter("uint256", "amount", 2)]
    public BigInteger Amount { get; set; }
}

var input = new TransferFunction
{
    To = "1234567890abcdef1234567890abcdef12345678",
    Amount = new BigInteger(5000)
};

var result = new FunctionCallEncoder().EncodeRequest(input, "a9059cbb");
```

Source: `AbiEncodingDocExampleTests.ShouldEncodeUsingParameterAttributes`

## Output decoding

```csharp
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
var balance = (BigInteger)result[0].Result; // 69
```

Source: `AbiEncodingDocExampleTests.ShouldDecodeFunctionOutput`

## Event topic decoding

```csharp
[Event("Transfer")]
public class TransferEventDTO
{
    [Parameter("address", "_from", 1, true)]
    public string From { get; set; }

    [Parameter("address", "_to", 2, true)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 3, true)]
    public BigInteger Value { get; set; }
}

var topics = new[]
{
    "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef",
    "0x0000000000000000000000000000000000000000000000000000000000000000",
    "0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91",
    "0x0000000000000000000000000000000000000000000000400000402000000001"
};

var transferDto = new TransferEventDTO();
new EventTopicDecoder().DecodeTopics(transferDto, topics, "0x");
```

Source: `AbiEncodingDocExampleTests.ShouldDecodeTransferEventTopic`

## ABI JSON deserialization

```csharp
var abi = @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],
""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";

var des = new ABIJsonDeserialiser();
var contract = des.DeserialiseContract(abi);
// contract.Functions, contract.Events
```

Source: `AbiEncodingDocExampleTests.ShouldDeserializeContractAbi`

## Custom error decoding

```csharp
var error = new ErrorABI("InsufficientBalance");
error.InputParameters = new[]
{
    new Parameter("address", "account", 1),
    new Parameter("uint256", "balance", 2)
};

var errorSelector = error.Sha3Signature;
var encodedData = "0x" + errorSelector +
    "000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91" +
    "0000000000000000000000000000000000000000000000000000000000000064";

var decoder = new FunctionCallDecoder();
var decoded = decoder.DecodeError(error, encodedData);
// decoded[0].Result = address, decoded[1].Result = BigInteger(100)
```

Source: `AbiEncodingDocExampleTests.ShouldDecodeCustomError`

## Individual type encoding

```csharp
var addressEncoded = new AddressType().Encode("1234567890abcdef1234567890abcdef12345678");
var intEncoded = new IntType("uint256").Encode(new BigInteger(42));
var boolEncoded = new BoolType().Encode(true);

var bytes32Value = new byte[32];
bytes32Value[0] = 0xAB;
var bytes32Encoded = new Bytes32Type("bytes32").Encode(bytes32Value);
// All produce 32-byte padded output
```

Source: `AbiEncodingDocExampleTests.ShouldEncodeIndividualTypesWithPadding`

## Required usings

```csharp
using Nethereum.ABI;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Numerics;
```
