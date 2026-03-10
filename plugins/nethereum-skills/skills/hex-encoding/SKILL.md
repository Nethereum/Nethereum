---
name: hex-encoding
description: Convert between bytes, strings, and hex in Nethereum. Use when the user needs hex conversion, byte array to hex, hex to bytes, HexBigInteger, hex prefix handling, or UTF-8 hex encoding.
user-invocable: true
---

# Hex Encoding with Nethereum

NuGet: `Nethereum.Hex` (included in `Nethereum.Web3`)

## Bytes to hex

```csharp
var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

var hexWithPrefix = data.ToHex(prefix: true);    // "0xdeadbeef"
var hexWithoutPrefix = data.ToHex(prefix: false); // "deadbeef"
```

Source: `HexConversionDocExampleTests.ShouldConvertBytesToHex`

## Hex to bytes

```csharp
var hex = "0xdeadbeef";
var bytes = hex.HexToByteArray();
// { 0xDE, 0xAD, 0xBE, 0xEF }
```

Source: `HexConversionDocExampleTests.ShouldConvertHexToBytes`

## Prefix handling

```csharp
// Ensure prefix is present (idempotent)
"deadbeef".EnsureHexPrefix();   // "0xdeadbeef"
"0xdeadbeef".EnsureHexPrefix(); // "0xdeadbeef"

// Check prefix
"0xdeadbeef".HasHexPrefix(); // true

// Remove prefix
"0xdeadbeef".RemoveHexPrefix(); // "deadbeef"
```

Source: `HexConversionDocExampleTests.ShouldEnsureHexPrefix`, `ShouldCheckAndRemoveHexPrefix`

## Hex comparison (case-insensitive)

```csharp
var hex1 = "0xDeAdBeEf";
var hex2 = "0xdeadbeef";

hex1.IsTheSameHex(hex2); // true
```

Source: `HexConversionDocExampleTests.ShouldCompareHexStrings`

## HexBigInteger for gas and value

```csharp
var fromNumber = new HexBigInteger(new BigInteger(1_000_000));
var fromHex = new HexBigInteger("0xf4240");

fromNumber.Value;    // BigInteger 1000000
fromNumber.HexValue; // "0xf4240"
fromHex.Value;       // BigInteger 1000000
```

Source: `HexConversionDocExampleTests.ShouldCreateHexBigIntegerFromBothFormats`

## UTF-8 encoding

```csharp
var text = "Hello Ethereum";
var hex = text.ToHexUTF8();
var decoded = hex.HexToUTF8String();
// decoded == "Hello Ethereum"
```

Source: `HexConversionDocExampleTests.ShouldEncodeDecodeUtf8AsHex`

## Required usings

```csharp
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using System.Numerics;
```
