---
name: eip712-signing
description: Sign and verify EIP-712 typed structured data using Nethereum (.NET). Use this skill whenever the user asks about EIP-712, typed data signing, structured data signing, domain separator, SignTypedData, ERC-2612 Permit signatures, or off-chain message signing with typed schemas using C# or .NET.
user-invocable: true
---

# EIP-712 Typed Data Signing with Nethereum

## Package

```
Nethereum.Signer.EIP712
```

## Namespaces

```csharp
using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
```

## Core Concepts

EIP-712 signs structured data with a domain separator, producing signatures that wallets can display in human-readable form. Nethereum maps EIP-712 structs to C# classes using attributes.

## Step 1: Define Typed Structs

Each EIP-712 struct becomes a C# class with `[Struct]` and `[Parameter]` attributes.

```csharp
[Struct("Person")]
public class Person
{
    [Parameter("string", "name", 1)]
    public string Name { get; set; }

    [Parameter("address", "wallet", 2)]
    public string Wallet { get; set; }
}

[Struct("Mail")]
public class Mail
{
    [Parameter("tuple", "from", 1, "Person")]
    public Person From { get; set; }

    [Parameter("tuple", "to", 2, "Person")]
    public Person To { get; set; }

    [Parameter("string", "contents", 3)]
    public string Contents { get; set; }
}
```

Rules:
- Use `tuple` as the Solidity type for nested structs, with the struct name as the fourth `[Parameter]` argument.
- The `[Struct("Name")]` value must match the EIP-712 type name exactly.
- Parameter order numbers (1, 2, 3...) must match the EIP-712 field order.

## Step 2: Create TypedData with Domain

```csharp
var typedData = new TypedData<Domain>
{
    Domain = new Domain
    {
        Name = "Ether Mail",
        Version = "1",
        ChainId = 1,
        VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
    },
    Types = MemberDescriptionFactory.GetTypesMemberDescription(
        typeof(Domain), typeof(Mail), typeof(Person)),
    PrimaryType = nameof(Mail),
};
```

`MemberDescriptionFactory.GetTypesMemberDescription` auto-generates the type schema from C# classes. Pass `typeof(Domain)` plus all struct types.

Domain fields: `Name`, `Version`, `ChainId`, `VerifyingContract`, `Salt` (optional).

## Step 3: Sign and Recover

```csharp
var signer = new Eip712TypedDataSigner();
var key = new EthECKey("private-key-hex");

var mail = new Mail
{
    From = new Person
    {
        Name = "Cow",
        Wallet = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826"
    },
    To = new Person
    {
        Name = "Bob",
        Wallet = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB"
    },
    Contents = "Hello, Bob!"
};

var signature = signer.SignTypedDataV4(mail, typedData, key);
var recoveredAddress = signer.RecoverFromSignatureV4(mail, typedData, signature);

// Verify
bool isValid = key.GetPublicAddress().IsTheSameAddress(recoveredAddress);
```

The signature is a `0x`-prefixed hex string (132 chars = 65 bytes).

## Sign from JSON (No C# Types)

When receiving EIP-712 data as JSON (e.g., from WalletConnect):

```csharp
var typedDataJson = @"{
    'domain': {
        'chainId': 1,
        'name': 'Ether Mail',
        'verifyingContract': '0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC',
        'version': '1'
    },
    'message': {
        'contents': 'Hello, Bob!',
        'from': { 'name': 'Cow', 'wallet': '0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826' },
        'to': { 'name': 'Bob', 'wallet': '0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB' }
    },
    'primaryType': 'Mail',
    'types': {
        'EIP712Domain': [
            { 'name': 'name', 'type': 'string' },
            { 'name': 'version', 'type': 'string' },
            { 'name': 'chainId', 'type': 'uint256' },
            { 'name': 'verifyingContract', 'type': 'address' }
        ],
        'Mail': [
            { 'name': 'from', 'type': 'Person' },
            { 'name': 'to', 'type': 'Person' },
            { 'name': 'contents', 'type': 'string' }
        ],
        'Person': [
            { 'name': 'name', 'type': 'string' },
            { 'name': 'wallet', 'type': 'address' }
        ]
    }
}";

var key = new EthECKey("private-key-hex");
var signer = new Eip712TypedDataSigner();

var signature = signer.SignTypedDataV4(typedDataJson, key);
var recoveredAddress = signer.RecoverFromSignatureV4(typedDataJson, signature);
```

## ERC-2612 Permit Signature

Define the Permit struct:

```csharp
[Struct("Permit")]
public class Permit
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; }

    [Parameter("address", "spender", 2)]
    public string Spender { get; set; }

    [Parameter("uint256", "value", 3)]
    public BigInteger Value { get; set; }

    [Parameter("uint256", "nonce", 4)]
    public BigInteger Nonce { get; set; }

    [Parameter("uint256", "deadline", 5)]
    public BigInteger Deadline { get; set; }
}
```

Sign a permit:

```csharp
var typedData = new TypedData<Domain>
{
    Domain = new Domain
    {
        Name = "MyToken",
        Version = "1",
        ChainId = 1,
        VerifyingContract = "0x1234567890abcdef1234567890abcdef12345678"
    },
    Types = MemberDescriptionFactory.GetTypesMemberDescription(
        typeof(Domain), typeof(Permit)),
    PrimaryType = nameof(Permit),
};

var permit = new Permit
{
    Owner = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826",
    Spender = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB",
    Value = BigInteger.Parse("1000000000000000000"),
    Nonce = 0,
    Deadline = BigInteger.Parse("1000000000000")
};

var key = new EthECKey("private-key-hex");
var signer = new Eip712TypedDataSigner();
var signature = signer.SignTypedDataV4(permit, typedData, key);
```

## Auto-Generated Schema Shortcut

For simple structs without nesting, skip manual `TypedData` construction:

```csharp
var domain = new Domain
{
    Name = "Ether Mail",
    Version = "1",
    ChainId = 1,
    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
};

var signer = new Eip712TypedDataSigner();
var key = new EthECKey("private-key-hex");

var signature = signer.SignTypedData<Permit, Domain>(permit, domain, "Permit", key);
```

Manual verification of auto-generated signatures:

```csharp
var encodedData = Eip712TypedDataEncoder.Current
    .EncodeTypedData(permit, domain, "Permit");
var recoveredAddress = new MessageSigner().EcRecover(
    Sha3Keccack.Current.CalculateHash(encodedData), signature);
```

## Quick Reference

| Method | Use Case |
|---|---|
| `SignTypedDataV4(message, typedData, key)` | Sign with C# typed structs |
| `RecoverFromSignatureV4(message, typedData, sig)` | Recover address from typed struct signature |
| `SignTypedDataV4(json, key)` | Sign from raw JSON |
| `RecoverFromSignatureV4(json, sig)` | Recover address from JSON signature |
| `SignTypedData<T, TDomain>(msg, domain, name, key)` | Sign with auto-generated schema |

## Common Patterns

### Verify a Signature

```csharp
var expectedAddress = "0x...";
var recoveredAddress = signer.RecoverFromSignatureV4(message, typedData, signature);
bool isValid = expectedAddress.IsTheSameAddress(recoveredAddress);
```

### Multi-Chain Domain

```csharp
var domain = new Domain
{
    Name = "My Dapp",
    Version = "2",
    ChainId = 137,  // Polygon
    VerifyingContract = "0x1111111111111111111111111111111111111111"
};
```

## Verified Source

All patterns from `tests/Nethereum.Signer.UnitTests/Eip712DocExampleTests.cs`.
