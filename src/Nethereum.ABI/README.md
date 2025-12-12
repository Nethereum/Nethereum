# Nethereum.ABI

**Encoding and decoding of ABI Types, functions, events of Ethereum contracts**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.ABI.svg)](https://www.nuget.org/packages/Nethereum.ABI/)

## Overview

Nethereum.ABI is the core package for Ethereum's Application Binary Interface (ABI) encoding and decoding in .NET. It provides comprehensive support for encoding function calls, decoding function outputs, processing event logs, **EIP-712 typed data signing**, and handling all Solidity data types including complex structures like tuples and dynamic arrays.

This package is fundamental to all smart contract interactions in Nethereum, as it translates between .NET objects and the binary format Ethereum uses for contract communication.

## Installation

```bash
dotnet add package Nethereum.ABI
```

## Key Concepts

### Application Binary Interface (ABI)
The ABI is a JSON specification that describes:
- **Functions**: Input parameters, output parameters, and function signatures
- **Events**: Indexed and non-indexed parameters for log filtering and decoding
- **Errors**: Custom error types and their parameters
- **Types**: Complete type system including elementary types, arrays, structs (tuples)

### Function Encoding
Function calls are encoded as:
1. **Function Selector**: First 4 bytes of Keccak-256 hash of the function signature
2. **Encoded Parameters**: ABI-encoded input parameters (32-byte aligned)

### Event Decoding
Events are stored in transaction logs with:
- **Topics**: Up to 4 indexed parameters (including event signature hash)
- **Data**: Non-indexed parameters (ABI-encoded)

### EIP-712 Typed Data
Structured data hashing and signing standard that enables:
- **Human-readable signatures**: Users can see what they're signing
- **Domain separation**: Prevents replay attacks across different dApps
- **Typed structs**: Support for complex nested data structures
- **Used by**: MetaMask, Permit (EIP-2612), Uniswap, OpenSea, and many more

### Type System
Nethereum.ABI supports all Solidity types:
- **Elementary**: `uint256`, `int256`, `address`, `bool`, `bytes`, `bytesN`, `string`
- **Fixed Arrays**: `uint256[20]`, `address[5]`
- **Dynamic Arrays**: `uint256[]`, `string[]`
- **Tuples**: Complex structs with nested components

## Quick Start

### Basic Function Encoding

```csharp
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;

// Create encoder
var functionCallEncoder = new FunctionCallEncoder();

// Define function signature and parameters
var sha3Signature = "c6888fa1";  // First 8 hex chars of Keccak-256("functionName(paramTypes)")
var parameters = new[]
{
    new Parameter("address", "recipient"),
    new Parameter("uint256", "amount")
};

// Encode function call
string encoded = functionCallEncoder.EncodeRequest(
    sha3Signature,
    parameters,
    "0x1234567890abcdef1234567890abcdef12345678",  // recipient
    1000000000000000000  // amount (1 ETH in wei)
);
// Result: "0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef12345678000000000000000000000000000000000000000000000000001e4c89d6c7e400"
```

### Decoding Function Output

```csharp
// From test: FunctionEncodingTests.cs
var functionCallDecoder = new FunctionCallDecoder();

var outputParameters = new[]
{
    new ParameterOutput()
    {
        Parameter = new Parameter("uint[]", "numbers")
        {
            DecodedType = typeof(List<int>)
        }
    }
};

var result = functionCallDecoder.DecodeOutput(
    "0x0000000000000000000000000000000000000000000000000000000000000020" +
    "0000000000000000000000000000000000000000000000000000000000000003" +
    "0000000000000000000000000000000000000000000000000000000000000000" +
    "0000000000000000000000000000000000000000000000000000000000000001" +
    "0000000000000000000000000000000000000000000000000000000000000002",
    outputParameters
);

var numbers = (List<int>)result[0].Result;
// numbers: [0, 1, 2]
```

## Usage Examples

### Example 1: Encoding Multiple Types Including Dynamic Strings

From test: `FunctionEncodingTests.cs:125`

```csharp
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;

var functionCallEncoder = new FunctionCallEncoder();
var sha3Signature = "c6888fa1";

var inputsParameters = new[]
{
    new Parameter("string", "greeting"),
    new Parameter("uint[20]", "numbers"),
    new Parameter("string", "farewell")
};

var array = new uint[20];
for (uint i = 0; i < 20; i++)
    array[i] = i + 234567;

string encoded = functionCallEncoder.EncodeRequest(
    sha3Signature,
    inputsParameters,
    "hello",      // Dynamic string (pointer to data)
    array,        // Fixed-size array (inline)
    "world"       // Dynamic string (pointer to data)
);

// Result starts with function selector, followed by:
// - Pointer to "hello" data
// - 20 uint256 values inline
// - Pointer to "world" data
// - Actual "hello" string data
// - Actual "world" string data
```

### Example 2: Attribute-Based Function Encoding

From test: `FunctionAttributeEncodingTests.cs:55`

```csharp
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;

[Function("multiply")]
public class MultiplyFunction : FunctionMessage
{
    [Parameter("uint256", "a", 1)]
    public int A { get; set; }
}

var input = new MultiplyFunction { A = 69 };
var encoder = new FunctionCallEncoder();
string encoded = encoder.EncodeRequest(input, "c6888fa1");

// Result: "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045"
// 69 decimal = 0x45 hex, padded to 32 bytes
```

### Example 3: Decoding Event Topics (Transfer Event)

From test: `EventTopicDecoderTests.cs:13`

```csharp
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

[Event("Transfer")]
public class TransferEvent
{
    [Parameter("address", "_from", 1, indexed: true)]
    public string From { get; set; }

    [Parameter("address", "_to", 2, indexed: true)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 3, indexed: true)]
    public BigInteger Value { get; set; }
}

var topics = new[]
{
    "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef",  // Event signature
    "0x0000000000000000000000000000000000000000000000000000000000000000",  // from (zero address)
    "0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91",  // to
    "0x0000000000000000000000000000000000000000000000400000402000000001"   // value
};

var data = "0x";  // No non-indexed data

var transferEvent = new TransferEvent();
new EventTopicDecoder().DecodeTopics(transferEvent, topics, data);

// transferEvent.From: "0x0000000000000000000000000000000000000000"
// transferEvent.To: "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91"
// transferEvent.Value: 1180591691223594434561
```

### Example 4: Simple ABI Encoding with ABIEncode

From test: `AbiEncodeTests.cs:12`

```csharp
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;

var abiEncode = new ABIEncode();

// Encode multiple values with explicit types
byte[] encoded = abiEncode.GetABIEncoded(
    new ABIValue("string", "hello"),
    new ABIValue("int", 69),
    new ABIValue("string", "world")
);

string hexResult = encoded.ToHex(true);
// Result: "0x0000000000000000000000000000000000000000000000000000000000000060..."
// Includes pointers to dynamic data and the actual string data
```

### Example 5: Encoding with Parameter Attributes

From test: `AbiEncodeTests.cs:34`

```csharp
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;

public class TestParamsInput
{
    [Parameter("string", 1)]
    public string First { get; set; }

    [Parameter("int256", 2)]
    public int Second { get; set; }

    [Parameter("string", 3)]
    public string Third { get; set; }
}

var abiEncode = new ABIEncode();
var input = new TestParamsInput
{
    First = "hello",
    Second = 69,
    Third = "world"
};

byte[] encoded = abiEncode.GetABIParamsEncoded(input);
// Automatically encodes based on Parameter attributes
```

### Example 6: Deserializing Contract ABI JSON

From test: `FunctionAttributeEncodingTests.cs:14`

```csharp
using Nethereum.ABI.ABIDeserialisation;
using System.Linq;

var abi = @"[
    {
        ""constant"": false,
        ""inputs"": [{""name"": ""a"", ""type"": ""uint256""}],
        ""name"": ""multiply"",
        ""outputs"": [{""name"": ""d"", ""type"": ""uint256""}],
        ""type"": ""function""
    }
]";

var deserializer = new ABIJsonDeserialiser();
var contract = deserializer.DeserialiseContract(abi);

var multiplyFunction = contract.Functions.FirstOrDefault(x => x.Name == "multiply");
// multiplyFunction.Sha3Signature: "c6888fa1"
// multiplyFunction.Constant: false
// multiplyFunction.InputParameters[0].Type: "uint256"
```

### Example 7: Complex Tuples with Nested Arrays

From test: `AbiDeserialiseTuplesTests.cs:22`

```csharp
using Nethereum.ABI.ABIDeserialisation;
using System.Linq;

// Complex ABI with nested tuple containing array of tuples
var abi = @"[{
    ""constant"": false,
    ""inputs"": [{
        ""components"": [
            {""name"": ""id"", ""type"": ""uint256""},
            {
                ""components"": [
                    {""name"": ""id"", ""type"": ""uint256""},
                    {""name"": ""productId"", ""type"": ""uint256""},
                    {""name"": ""quantity"", ""type"": ""uint256""}
                ],
                ""name"": ""lineItem"",
                ""type"": ""tuple[]""
            },
            {""name"": ""customerId"", ""type"": ""uint256""}
        ],
        ""name"": ""purchaseOrder"",
        ""type"": ""tuple""
    }],
    ""name"": ""SetPurchaseOrder"",
    ""outputs"": [],
    ""type"": ""function""
}]";

var contractAbi = new ABIJsonDeserialiser().DeserialiseContract(abi);
var functionABI = contractAbi.Functions.FirstOrDefault(e => e.Name == "SetPurchaseOrder");

// Function signature includes full tuple structure
// functionABI.Sha3Signature: "0cc400bd"
```

### Example 8: Encoding Individual Types

From test: `FunctionEncodingTests.cs:79-107`

```csharp
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;

var encoder = new FunctionCallEncoder();
var signature = "c6888fa1";

// Address encoding
var addressParam = new[] { new Parameter("address", "recipient") };
string encodedAddress = encoder.EncodeRequest(
    signature,
    addressParam,
    "0x1234567890abcdef1234567890abcdef12345678"
);
// Result: "0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef12345678"

// Boolean encoding
var boolParam = new[] { new Parameter("bool", "flag") };
string encodedBool = encoder.EncodeRequest(signature, boolParam, true);
// Result: "0xc6888fa10000000000000000000000000000000000000000000000000000000000000001"

// Integer encoding
var intParam = new[] { new Parameter("int", "number") };
string encodedInt = encoder.EncodeRequest(signature, intParam, 69);
// Result: "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045"
// Note: 69 decimal = 0x45 hex
```

### Example 9: EIP-712 Typed Data with Simple Structs

From test: `Eip712TypedDataSignerSimpleScenarioTest.cs:66`

```csharp
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer.EIP712;
using System.Collections.Generic;

// Define your domain-specific structs
[Struct("Person")]
public class Person
{
    [Parameter("string", "name", 1)]
    public string Name { get; set; }

    [Parameter("address[]", "wallets", 2)]
    public List<string> Wallets { get; set; }
}

[Struct("Mail")]
public class Mail
{
    [Parameter("tuple", "from", 1, "Person")]
    public Person From { get; set; }

    [Parameter("tuple[]", "to", 2, "Person[]")]
    public List<Person> To { get; set; }

    [Parameter("string", "contents", 3)]
    public string Contents { get; set; }
}

// Create typed data definition
var typedData = new TypedData<Domain>
{
    Domain = new Domain
    {
        Name = "Ether Mail",
        Version = "1",
        ChainId = 1,
        VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
    },
    Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Mail), typeof(Person)),
    PrimaryType = nameof(Mail),
};

// Create message
var mail = new Mail
{
    From = new Person
    {
        Name = "Cow",
        Wallets = new List<string>
        {
            "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826",
            "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF"
        }
    },
    To = new List<Person>
    {
        new Person
        {
            Name = "Bob",
            Wallets = new List<string>
            {
                "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB",
                "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57",
                "0xB0B0b0b0b0b0B000000000000000000000000000"
            }
        }
    },
    Contents = "Hello, Bob!"
};

// Set the message
typedData.SetMessage(mail);

// Sign using private key
var signer = new Eip712TypedDataSigner();
var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
string signature = signer.SignTypedDataV4(mail, typedData, key);
// signature: "0x943393c998ab7e067d2875385e2218c9b3140f563694267ac9f6276a9fcc53e1..."

// Recover signer address from signature
string recoveredAddress = signer.RecoverFromSignatureV4(mail, typedData, signature);
// recoveredAddress matches key.GetPublicAddress()
```

### Example 10: EIP-712 Typed Data from JSON

From test: `Eip712TypedDataSignerTest.cs:107`

```csharp
using Nethereum.ABI.EIP712;
using Nethereum.Signer.EIP712;

// EIP-712 typed data as JSON (MetaMask format)
var typedDataJson = @"{
    'domain': {
        'chainId': 1,
        'name': 'Ether Mail',
        'verifyingContract': '0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC',
        'version': '1'
    },
    'message': {
        'contents': 'Hello, Bob!',
        'from': {
            'name': 'Cow',
            'wallets': [
                '0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826',
                '0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF'
            ]
        },
        'to': [{
            'name': 'Bob',
            'wallets': [
                '0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB',
                '0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57',
                '0xB0B0b0b0b0b0B000000000000000000000000000'
            ]
        }]
    },
    'primaryType': 'Mail',
    'types': {
        'EIP712Domain': [
            {'name': 'name', 'type': 'string'},
            {'name': 'version', 'type': 'string'},
            {'name': 'chainId', 'type': 'uint256'},
            {'name': 'verifyingContract', 'type': 'address'}
        ],
        'Mail': [
            {'name': 'from', 'type': 'Person'},
            {'name': 'to', 'type': 'Person[]'},
            {'name': 'contents', 'type': 'string'}
        ],
        'Person': [
            {'name': 'name', 'type': 'string'},
            {'name': 'wallets', 'type': 'address[]'}
        ]
    }
}";

// Deserialize and encode
var rawTypedData = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(typedDataJson);
var signer = new Eip712TypedDataSigner();
byte[] encodedTypedData = signer.EncodeTypedDataRaw(rawTypedData);

// Sign directly from JSON
var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
string signature = signer.SignTypedDataV4(rawTypedData, key);
```

### Example 11: EIP-712 with Complex Nested Structures

From test: `EIP712TypeDataSignatureMultipleComplexInnerObjects.cs:13`

```csharp
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using System.Numerics;

[Struct("UsageLimit")]
public class UsageLimit
{
    [Parameter("uint8", "limitType", 1)]
    public byte LimitType { get; set; }

    [Parameter("uint256", "limit", 2)]
    public BigInteger Limit { get; set; }

    [Parameter("uint256", "period", 3)]
    public BigInteger Period { get; set; }
}

[Struct("Constraint")]
public class Constraint
{
    [Parameter("uint8", "condition", 1)]
    public byte Condition { get; set; }

    [Parameter("uint64", "index", 2)]
    public ulong Index { get; set; }

    [Parameter("bytes32", "refValue", 3)]
    public byte[] RefValue { get; set; }
}

[Struct("CallSpec")]
public class CallSpec
{
    [Parameter("address", "target", 1)]
    public string Target { get; set; }

    [Parameter("bytes4", "selector", 2)]
    public byte[] Selector { get; set; }

    [Parameter("uint256", "maxValuePerUse", 3)]
    public BigInteger MaxValuePerUse { get; set; }

    [Parameter("tuple", "valueLimit", 4, structTypeName: "UsageLimit")]
    public UsageLimit ValueLimit { get; set; }

    [Parameter("tuple[]", "constraints", 5, structTypeName: "Constraint[]")]
    public List<Constraint> Constraints { get; set; }
}

[Struct("SessionSpec")]
public class SessionSpec
{
    [Parameter("address", "signer", 1)]
    public string Signer { get; set; }

    [Parameter("uint256", "expiresAt", 2)]
    public BigInteger ExpiresAt { get; set; }

    [Parameter("tuple[]", "callPolicies", 3, structTypeName: "CallSpec[]")]
    public List<CallSpec> CallPolicies { get; set; }
}

// This demonstrates deep nesting: SessionSpec contains array of CallSpec,
// each CallSpec contains UsageLimit struct and array of Constraint structs
// Perfect for complex DeFi protocols, account abstraction, session keys, etc.
```

### Example 12: EIP-712 Encoding and Hashing

From test: `Eip712TypedDataEncoder.cs`

```csharp
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;

var encoder = new Eip712TypedDataEncoder();

// Encode from typed data
var mail = new Mail { /* ... */ };
var typedData = new TypedData<Domain> { /* ... */ };
byte[] encoded = encoder.EncodeTypedData(mail, typedData);

// Encode and hash in one operation (for signing)
byte[] hash = encoder.EncodeAndHashTypedData(mail, typedData);

// Encode directly from JSON
string json = /* EIP-712 JSON */;
byte[] encodedFromJson = encoder.EncodeTypedData(json);
byte[] hashFromJson = encoder.EncodeAndHashTypedData(json);

// The hash is what gets signed with ECDSA
string hashHex = hash.ToHex(true);
```

### Example 13: Error Handling for Invalid Parameters

From test: `FunctionEncodingTests.cs:161`

```csharp
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;

var encoder = new FunctionCallEncoder();
var signature = "c6888fa1";
var parameters = new[] { new Parameter("address", "_address1") };

try
{
    string encoded = encoder.EncodeRequest(signature, parameters, (object)null);
}
catch (AbiEncodingException ex)
{
    // ex.Message: "An error occurred encoding abi value. Order: '1', Type: 'address',
    //              Value: 'null'. Ensure the value is valid for the abi type."
    Console.WriteLine(ex.Message);
}
```

## API Reference

### Core Classes

#### `ABIEncode`
- `GetABIEncoded(params ABIValue[] abiValues)` - Encode values with explicit types
- `GetABIEncoded(params object[] values)` - Encode values with automatic type detection
- `GetABIParamsEncoded<T>(T input)` - Encode object using Parameter attributes
- `GetABIEncodedPacked(params ABIValue[] abiValues)` - Packed encoding (no padding)
- `GetSha3ABIEncoded(...)` - Encode and hash in one operation

#### `FunctionCallEncoder`
- `EncodeRequest(string sha3Signature, Parameter[] parameters, params object[] values)` - Encode function call
- `EncodeRequest<T>(T functionInput, string sha3Signature)` - Encode using attributes

#### `FunctionCallDecoder`
- `DecodeOutput(string output, params Parameter[] parameters)` - Decode function return values
- `DecodeFunctionOutput<T>(string output)` - Decode using attributes

#### `EventTopicDecoder`
- `DecodeTopics(object destination, string[] topics, string data)` - Decode event log into object
- `DecodeTopics<T>(string[] topics, string data)` - Decode event log to typed object

#### `ABIJsonDeserialiser`
- `DeserialiseContract(string abi)` - Parse contract ABI JSON
- `DeserialiseContract(JArray abi)` - Parse from JArray
- Produces `ContractABI` with Functions, Events, Errors, Constructor

#### `Eip712TypedDataEncoder`
- `EncodeTypedData<T, TDomain>(T message, TypedData<TDomain> typedData)` - Encode typed data with message
- `EncodeTypedData(string json)` - Encode from EIP-712 JSON
- `EncodeAndHashTypedData(...)` - Encode and hash for signing
- `EncodeTypedDataRaw(TypedDataRaw typedData)` - Low-level encoding

#### `Eip712TypedDataSigner` (in Nethereum.Signer)
- `SignTypedDataV4<T>(T message, TypedData<Domain> typedData, EthECKey key)` - Sign EIP-712 data
- `RecoverFromSignatureV4<T>(T message, TypedData<Domain> typedData, string signature)` - Recover signer
- `SignTypedDataV4(TypedDataRaw typedData, EthECKey key)` - Sign from raw typed data

### Encoding Attributes

- `[Function("name")]` - Mark class as function definition
- `[Event("name")]` - Mark class as event definition
- `[Struct("name")]` - Mark class as EIP-712 struct
- `[Parameter("type", "name", order, indexed)]` - Mark property as parameter
- `[FunctionOutput]` - Mark class can be used for output decoding

### EIP-712 Classes

- `TypedData<TDomain>` - Typed data with domain separation
- `Domain` - EIP-712 domain (name, version, chainId, verifyingContract, salt)
- `MemberDescription` - Type member definition (name, type)
- `MemberDescriptionFactory` - Generate type descriptions from .NET types
- `MemberValue` - Runtime value for encoding
- `TypedDataRaw` - Raw typed data without generics

## Related Packages

- **Nethereum.Hex** - Hexadecimal encoding used throughout ABI operations
- **Nethereum.Util** - Keccak-256 hashing for function/event signatures
- **Nethereum.Signer** - EIP-712 signing and signature recovery
- **Nethereum.Contracts** - High-level contract interaction built on ABI encoding
- **Nethereum.RPC** - JSON-RPC calls that use ABI-encoded data

## Important Notes

### Function Signature Calculation
Function signatures are the first 4 bytes of Keccak-256 hash of the canonical function signature:
```
Keccak256("transfer(address,uint256)") → 0xa9059cbb2ab09eb219583f4a59a5d0623ade346d962bcd4e46b11da047c9049b
Function selector: 0xa9059cbb (first 4 bytes)
```

### Event Signature Calculation
Event signatures are the full 32 bytes of Keccak-256 hash of the canonical event signature:
```
Keccak256("Transfer(address,address,uint256)") → 0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef
Event topic[0]: Full hash (identifies the event)
```

### EIP-712 Domain Separation
The domain separator prevents signature replay attacks across:
- Different contracts (via `verifyingContract` address)
- Different chains (via `chainId`)
- Different versions (via `version`)
- Different applications (via `name`)

Formula: `Keccak256(encodeData("EIP712Domain", domain))`

### EIP-712 Structured Data Hash
Final hash signed by user:
```
Keccak256("\x19\x01" + domainSeparator + hashStruct(message))
```
Where:
- `\x19\x01` is the version byte for structured data
- `domainSeparator` is the hash of the domain
- `hashStruct(message)` is the hash of the primary message type

### Dynamic vs Static Types
- **Static types** (uint256, address, bool, bytesN, fixed arrays): Encoded inline
- **Dynamic types** (string, bytes, dynamic arrays, tuples with dynamic members): Encoded as pointer + data

### Type Canonicalization
When calculating signatures, types must be canonical:
- `uint` → `uint256`
- `int` → `int256`
- No spaces: `transfer(address,uint256)` not `transfer(address, uint256)`

### Indexed Event Parameters
- Maximum 3 indexed parameters per event (topic[0] is always event signature)
- Indexed parameters are searchable/filterable in logs
- Non-indexed parameters are cheaper and stored in data field

### EIP-712 Use Cases
- **MetaMask Sign-In**: Authenticate users without gas
- **EIP-2612 Permit**: Gasless token approvals (USDC, DAI, UNI)
- **OpenSea/NFT Marketplaces**: Off-chain order signing
- **Uniswap Permit2**: Advanced approval management
- **Account Abstraction**: Session keys and delegated operations
- **DAO Voting**: Off-chain vote collection with on-chain execution

## Playground Examples

Runnable examples available at [Nethereum Playground](https://playground.nethereum.com/):

**Human-Readable ABI:**
- [Example 1069](https://playground.nethereum.com/csharp/id/1069) - Deployment, calls, and transactions using human-readable ABI format

**ABI Encoding:**
- [Example 1070](https://playground.nethereum.com/csharp/id/1070) - Encoding using ABI Values, Parameters and Default values
- [Example 1071](https://playground.nethereum.com/csharp/id/1071) - ABI Encoding Packed using ABI Values
- [Example 1072](https://playground.nethereum.com/csharp/id/1072) - ABI Encoding Packed using parameters
- [Example 1073](https://playground.nethereum.com/csharp/id/1073) - ABI Encoding Packed using default values

## Resources

- [Ethereum Contract ABI Specification](https://docs.soliditylang.org/en/latest/abi-spec.html)
- [EIP-712: Typed structured data hashing and signing](https://eips.ethereum.org/EIPS/eip-712)
- [EIP-2612: Permit - Token Approvals via Signatures](https://eips.ethereum.org/EIPS/eip-2612)
- [Solidity Documentation](https://docs.soliditylang.org/)
- [Nethereum Documentation](https://docs.nethereum.com/)
- [Source Code](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.ABI)
